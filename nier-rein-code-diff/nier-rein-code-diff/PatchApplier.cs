using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using nier_rein_code_diff.Compilation;
using nier_rein_code_diff.Models;
using nier_rein_code_diff.Support;

namespace nier_rein_code_diff
{
    class PatchApplier
    {
        private const string MasterDatabasePath_ = @"NierReincarnation.Core\Dark\DarkMasterMemoryDatabase.cs";
        private const string DarkPath_ = @"NierReincarnation.Core\Dark";
        private const string TablePath_ = DarkPath_ + @"\Tables";

        private static readonly Regex TableElementSplitter = new Regex(@"[A-Z][a-z]*");

        private readonly string _dumpCsPath;
        private readonly string _apiCodePath;

        public PatchApplier(string dumpCsPath, string apiCodePath)
        {
            _dumpCsPath = dumpCsPath;
            _apiCodePath = apiCodePath;
        }

        /// <summary>
        /// Apply patches from dump.cs to code base.
        /// </summary>
        /// <param name="diff"></param>
        public void Apply(DatabaseDiff diff)
        {
            ApplyTableDiff(diff.TableDiff);
            ApplyTableEntityDiff(diff.EntityDiff);  // TODO: Implement
        }

        #region Apply table diff

        // Update Dark*MemoryDatabase
        private void ApplyTableDiff(IList<TableDiff> tableDiff)
        {
            if (tableDiff.Count <= 0)
                return;

            var node = ParserSupport.ParseFile(Path.Combine(_apiCodePath, MasterDatabasePath_));

            var cl = node.FirstOrDefault(SyntaxNodeKind.NamespaceUnit).FirstOrDefault(SyntaxNodeKind.Class).FirstOrDefault(SyntaxNodeKind.ClassBlock);
            var initMethodNode = cl.Where(SyntaxNodeKind.Method).FirstOrDefault(x => x.FirstOrDefault(SyntaxNodeKind.MemberName).BuildFullString() == "Init")?.FirstOrDefault(SyntaxNodeKind.MethodBody);

            // Parse existing offsets
            var indexDic = new Dictionary<int, int>();
            for (var i = 0; i < cl.Nodes.Count; i++)
            {
                if (cl.Nodes[i].Kind != SyntaxNodeKind.Property)
                    continue;

                if (!int.TryParse(cl.Nodes[i - 1].FirstOrDefault(SyntaxNodeKind.CommentText).Text.TrimEnd()[2..], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var parsedOffset))
                    continue;

                indexDic[parsedOffset] = i - 1;
            }

            // Apply changed offsets
            var offsetArray = new int[Math.Max(indexDic.Keys.Max(), tableDiff.Select(x => x.DumpFieldInfo?.Offset ?? 0).Max()) / 8 + 1];
            foreach (var indexPair in indexDic)
                offsetArray[indexPair.Key / 8] = indexPair.Value;

            var newOffsetArray = new int[offsetArray.Length];
            Array.Copy(offsetArray, newOffsetArray, offsetArray.Length);

            foreach (var diff in tableDiff.Where(x => x.Type == DiffType.Changed))
            {
                // Update offset of table property
                var nodeIndex = offsetArray[diff.OwnFieldInfo.Offset / 8];
                cl.Nodes[nodeIndex].FirstOrDefault(SyntaxNodeKind.CommentText).Text = $"0x{diff.DumpFieldInfo.Offset:X}";

                if (newOffsetArray[diff.OwnFieldInfo.Offset / 8] == nodeIndex)
                    newOffsetArray[diff.OwnFieldInfo.Offset / 8] = 0;
                newOffsetArray[diff.DumpFieldInfo.Offset / 8] = nodeIndex;
            }

            // Apply new changes
            offsetArray = newOffsetArray;
            newOffsetArray = new int[offsetArray.Length];
            Array.Copy(offsetArray, newOffsetArray, offsetArray.Length);

            foreach (var diff in tableDiff.Where(x => x.Type == DiffType.New))
            {
                // Without the Init method, we can't add new table properties
                if (initMethodNode == null)
                    break;

                // Add table property
                var tempFieldOffset = diff.DumpFieldInfo.Offset;
                for (var i = tempFieldOffset; i >= 0x10; i -= 8)
                {
                    if (offsetArray[i / 8] != 0)
                        break;

                    tempFieldOffset -= 8;
                }

                var nodeIndex = tempFieldOffset == 8 ?
                    cl.Nodes.IndexOf(cl.FirstOrDefault(SyntaxNodeKind.Property)) - 1 :
                    offsetArray[tempFieldOffset / 8] + 2;

                cl.Nodes.Insert(nodeIndex, new SyntaxNode(SyntaxNodeKind.Comment) { Nodes = { new SyntaxNode(SyntaxNodeKind.DoubleSlash) { Text = "//", Trail = { new SyntaxNode(SyntaxNodeKind.WhiteSpace) { Text = " " } } }, new SyntaxNode(SyntaxNodeKind.CommentText) { Text = $"0x{diff.DumpFieldInfo.Offset:X}", Trail = { new SyntaxNode(SyntaxNodeKind.Identifier) { Text = "\r\n        " } } } } });
                cl.Nodes.Insert(nodeIndex + 1, new SyntaxNode(SyntaxNodeKind.Identifier) { Text = $"public {diff.DumpFieldInfo.Type} {diff.DumpFieldInfo.Name} {{ get; private set; }}", Trail = { new SyntaxNode(SyntaxNodeKind.Identifier) { Text = "\r\n        " } } });

                for (var i = tempFieldOffset == 8 ? 0x18 : tempFieldOffset + 8; i < offsetArray.Length * 8; i += 8)
                    if (offsetArray[i / 8] != 0)
                        offsetArray[i / 8] += 2;
                offsetArray[diff.DumpFieldInfo.Offset / 8] = nodeIndex;

                // Add table property initialization
                var lastNodeIndex = initMethodNode.Nodes.IndexOf(initMethodNode.FirstOrDefault(SyntaxNodeKind.CloseBrace));
                initMethodNode.Nodes.Insert(lastNodeIndex, new SyntaxNode(SyntaxNodeKind.Identifier) { Text = $"    {diff.DumpFieldInfo.Name} = ExtractTableData(header, databaseBinary, options, new Func<{diff.DumpFieldInfo.Type[..^5]}[], {diff.DumpFieldInfo.Name}>(data => new {diff.DumpFieldInfo.Name}(data)));\r\n        " });
            }

            node.ToFile(Path.Combine(_apiCodePath, MasterDatabasePath_));

            // Create new tables based on table differences
            CreateNewTables(tableDiff);
        }

        #region Create table and element

        private void CreateNewTables(IList<TableDiff> tableDiff)
        {
            foreach (var diff in tableDiff)
            {
                switch (diff.Type)
                {
                    case DiffType.New:
                        CreateTableClass(diff, out var elementNode);
                        CreateTableElementClass(diff, elementNode);
                        break;
                }
            }
        }

        private void CreateTableClass(TableDiff diff, out SyntaxNode elementNode)
        {
            elementNode = null;

            var path = Path.Combine(_apiCodePath, TablePath_, diff.DumpFieldInfo.Type + ".cs");
            if (File.Exists(path))
                return;

            // Parse table class
            var tableNode = ParserSupport.ParseClass(_dumpCsPath, ParserSupport.FindIndexByString($"public sealed class {diff.DumpFieldInfo.Type}"));

            // Parse table element class
            var tableType = diff.DumpFieldInfo.Type[..^5];
            elementNode = ParserSupport.ParseClass(_dumpCsPath, ParserSupport.FindIndexByString($"public class {tableType}"));

            // Create table class text
            var dumpTableElementClassNodes = elementNode.FirstOrDefault(SyntaxNodeKind.ClassBlock);

            // Collect primary and secondary key information
            var primaryTypes = new List<(string, string)>();
            var secondaryTypes = new List<(string, string)>();

            var attributeNodes = dumpTableElementClassNodes.Where(SyntaxNodeKind.Attribute);
            foreach (var attributeNode in attributeNodes)
            {
                var attributeName = attributeNode.FirstOrDefault(SyntaxNodeKind.Fqdn).BuildFullString();
                if (attributeName != "PrimaryKeyAttribute" && attributeName != "SecondaryKeyAttribute")
                    continue;

                IList<(string, string)> result;

                switch (attributeName)
                {
                    case "PrimaryKeyAttribute":
                        result = primaryTypes;
                        break;

                    case "SecondaryKeyAttribute":
                        result = secondaryTypes;
                        break;

                    default:
                        continue;
                }

                var nodeIndex = dumpTableElementClassNodes.Nodes.IndexOf(attributeNode);
                for (var i = 1; i < dumpTableElementClassNodes.Nodes.Count - nodeIndex; i++)
                    if (dumpTableElementClassNodes.Nodes[nodeIndex + i].Kind == SyntaxNodeKind.Property)
                    {
                        var propertyType = dumpTableElementClassNodes.Nodes[nodeIndex + i].FirstOrDefault(SyntaxNodeKind.Type).FirstOrDefault(SyntaxNodeKind.Fqdn).BuildFullString();
                        var propertyName = dumpTableElementClassNodes.Nodes[nodeIndex + i].FirstOrDefault(SyntaxNodeKind.MemberName).FirstOrDefault(SyntaxNodeKind.Identifier).Text;

                        result.Add((propertyType.Trim(), propertyName));

                        break;
                    }
            }

            var primarySelectorType = primaryTypes.Count < 2 ? primaryTypes[0].Item1 : $"({string.Join(",", primaryTypes.Select(x => x.Item1))})";
            var primarySelectorBody = primaryTypes.Count < 2 ? $"element.{primaryTypes[0].Item2}" : $"({string.Join(",", primaryTypes.Select(x => $"element.{x.Item2}"))})";
            var primarySelectorNames = primaryTypes.Select(x => x.Item2).ToArray();

            var secondarySelectorType = secondaryTypes.Count <= 0 ? string.Empty : secondaryTypes.Count < 2 ? secondaryTypes[0].Item1 : $"({string.Join(",", secondaryTypes.Select(x => x.Item1))})";
            var secondarySelectorBody = secondaryTypes.Count <= 0 ? string.Empty : secondaryTypes.Count < 2 ? $"element.{secondaryTypes[0].Item2}" : $"({string.Join(",", secondaryTypes.Select(x => $"element.{x.Item2}"))})";
            //var secondarySelectorNames = secondaryTypes.Select(x => x.Item2).ToArray();

            // Build index selector delegates
            var indexDelegateTemplate = File.ReadAllText("Templates\\IndexSelector.cs");
            var indexSelectorDelegates = string.Format(indexDelegateTemplate, tableType, primarySelectorType, "primary");

            if (!string.IsNullOrEmpty(secondarySelectorType))
                indexSelectorDelegates += string.Format(indexDelegateTemplate, tableType, secondarySelectorType, "secondary");

            // Build ctor body for index delegates
            var ctorIndexSelectorTemplate = File.ReadAllText("Templates\\CtorIndexSelector.cs");
            var ctorIndexSelectors = string.Format(ctorIndexSelectorTemplate, primarySelectorBody, "primary");

            if (!string.IsNullOrEmpty(secondarySelectorType))
                ctorIndexSelectors += string.Format(ctorIndexSelectorTemplate, secondarySelectorBody, "secondary");

            // Build method bodies
            var methods = string.Empty;
            var methodNodes = tableNode.FirstOrDefault(SyntaxNodeKind.ClassBlock).Where(SyntaxNodeKind.Method);
            foreach (var methodNode in methodNodes)
            {
                var methodBody = string.Empty;

                foreach (var parameter in methodNode.FirstOrDefault(SyntaxNodeKind.MethodBody).Where(SyntaxNodeKind.Parameter))
                {
                    if (!parameter.Has(SyntaxNodeKind.ParameterValue))
                        continue;

                    var parameterValue = parameter.FirstOrDefault(SyntaxNodeKind.ParameterValue).FirstOrDefault(SyntaxNodeKind.Identifier);
                    if (parameterValue != null)
                        parameterValue.Text = parameterValue.Text.ToLower();
                }

                var methodType = methodNode.FirstOrDefault(SyntaxNodeKind.Type).BuildFullString().Trim();
                var methodName = methodNode.FirstOrDefault(SyntaxNodeKind.MemberName).BuildFullString();

                var selectorRank = primarySelectorNames.All(x => methodName.Contains(x)) ? "primary" : "secondary";
                var selectorType = primarySelectorNames.All(x => methodName.Contains(x)) ? primarySelectorType : secondarySelectorType;

                if (methodName.StartsWith("FindBy") && methodType.StartsWith("RangeView"))
                    methodBody = $"return FindManyCore(data, {selectorRank}IndexSelector, Comparer<{selectorType}>.Default, key); ";
                else if (methodName.StartsWith("FindBy"))
                    methodBody = $"return FindUniqueCore(data, {selectorRank}IndexSelector, Comparer<{selectorType}>.Default, key); ";
                else if (methodName.StartsWith("FindRangeBy"))
                    methodBody = $"return FindUniqueRangeCore(data, {selectorRank}IndexSelector, Comparer<{selectorType}>.Default, min, max, ascendant); ";
                else if (methodName.StartsWith("TryFindBy"))
                    methodBody = $"return TryFindUniqueCore(data, {selectorRank}IndexSelector, Comparer<{selectorType}>.Default, key, out result); ";
                else if (methodName.StartsWith("FindClosestBy"))
                    methodBody = $"return FindUniqueClosestCore(data, {selectorRank}IndexSelector, Comparer<{selectorType}>.Default, key, selectLower); ";

                var bodyNodes = methodNode.FirstOrDefault(SyntaxNodeKind.MethodBody).Nodes;
                bodyNodes.Insert(bodyNodes.Count - 1, new SyntaxNode(SyntaxNodeKind.Identifier) { Text = methodBody });

                methods += "\r\n        " + methodNode.BuildFullString();
            }

            // Write table class text
            var templateText = File.ReadAllText("Templates\\TableClass.cs");
            var tableText = string.Format(templateText, tableType, indexSelectorDelegates, ctorIndexSelectors, methods);

            File.WriteAllText(path, tableText);
        }

        private void CreateTableElementClass(TableDiff diff, SyntaxNode elementNode)
        {
            var tableType = diff.DumpFieldInfo.Type[..^5];

            var path = Path.Combine(_apiCodePath, DarkPath_, tableType + ".cs");
            if (File.Exists(path))
                return;

            elementNode ??= ParserSupport.ParseClass(_dumpCsPath, ParserSupport.FindIndexByString($"public class {tableType}"));

            // Create table element class text
            var memTableName = string.Join("_", TableElementSplitter.Matches(tableType[6..]).Select(x => x.Value.ToLower()));

            var dumpTableElementClassNodes = elementNode.FirstOrDefault(SyntaxNodeKind.ClassBlock);

            var propertyText = string.Empty;
            var properties = dumpTableElementClassNodes.Where(SyntaxNodeKind.Property);
            var fieldIndex = dumpTableElementClassNodes.Nodes.IndexOf(dumpTableElementClassNodes.FirstOrDefault(SyntaxNodeKind.Field));
            for (var i = 0; i < properties.Length; i++)
            {
                var propertyType = string.Join("", properties[i].FirstOrDefault(SyntaxNodeKind.Type).FirstOrDefault(SyntaxNodeKind.Fqdn).Nodes).Trim();
                var propertyName = properties[i].FirstOrDefault(SyntaxNodeKind.MemberName).FirstOrDefault(SyntaxNodeKind.Identifier).Text;
                var offset = dumpTableElementClassNodes.Nodes[fieldIndex + i * 4 + 1].FirstOrDefault(SyntaxNodeKind.CommentText).Text;

                // Remap some types
                if (propertyType == "MPDateTime")
                    propertyType = "long";
                else
                    propertyType = propertyType.ToLower();

                propertyText += $"\r\n        [Key({i})]\r\n        public {propertyType} {propertyName} {{ get; set; }} // {offset}";
            }

            var templateText = File.ReadAllText("Templates\\TableElementClass.cs");
            var tableText = string.Format(templateText, tableType, memTableName, propertyText);

            // Write table element class text
            File.WriteAllText(path, tableText);
        }

        #endregion

        #endregion

        #region Apply table element diff

        private void ApplyTableEntityDiff(IList<EntityDiff> entityDiff)
        {
            foreach (var diff in entityDiff)
            {
                var path = Path.Combine(_apiCodePath, DarkPath_, diff.Name + ".cs");
                var mainNode = ParserSupport.ParseFile(path);

                var cl = mainNode.FirstOrDefault(SyntaxNodeKind.NamespaceUnit).FirstOrDefault(SyntaxNodeKind.Class).FirstOrDefault(SyntaxNodeKind.ClassBlock);

                // Parse existing offsets
                var indexDic = new Dictionary<int, int>();
                for (var i = 0; i < cl.Nodes.Count; i++)
                {
                    if (cl.Nodes[i].Kind != SyntaxNodeKind.Property)
                        continue;

                    if (!int.TryParse(cl.Nodes[i + 1].FirstOrDefault(SyntaxNodeKind.CommentText).Text.TrimEnd()[2..], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var parsedOffset))
                        continue;

                    indexDic[parsedOffset] = i + 1;
                }

                // Apply changed offsets
                var offsetArray = new int[Math.Max(indexDic.Keys.Max(), diff.FieldDiff.Select(x => x.DumpFieldInfo?.Offset ?? 0).Max()) / 8 + 1];
                foreach (var indexPair in indexDic)
                    offsetArray[indexPair.Key / 8] = indexPair.Value;

                var newOffsetArray = new int[offsetArray.Length];
                Array.Copy(offsetArray, newOffsetArray, offsetArray.Length);

                foreach (var fieldDiff in diff.FieldDiff.Where(x => x.Type == DiffType.Changed))
                {
                    // Update offset of element property
                    var nodeIndex = offsetArray[fieldDiff.OwnFieldInfo.Offset / 8];
                    cl.Nodes[nodeIndex].FirstOrDefault(SyntaxNodeKind.CommentText).Text = $"0x{fieldDiff.DumpFieldInfo.Offset:X}";

                    if (newOffsetArray[fieldDiff.OwnFieldInfo.Offset / 8] == nodeIndex)
                        newOffsetArray[fieldDiff.OwnFieldInfo.Offset / 8] = 0;
                    newOffsetArray[fieldDiff.DumpFieldInfo.Offset / 8] = nodeIndex;
                }

                // TODO: Apply new table members

                mainNode.ToFile(path);
            }
        }

        #endregion
    }
}
