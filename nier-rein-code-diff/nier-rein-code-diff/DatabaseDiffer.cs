using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using nier_rein_code_diff.Compilation;
using nier_rein_code_diff.Models;
using nier_rein_code_diff.Support;

namespace nier_rein_code_diff
{
    class DatabaseDiffer
    {
        private const string MasterDatabasePath_ = @"NierReincarnation.Core\Dark\DarkMasterMemoryDatabase.cs";
        private const string TablePath_ = @"NierReincarnation.Core\Dark";

        private readonly string _dumpCsPath;
        private readonly string _apiCodePath;

        public DatabaseDiffer(string dumpCsPath, string apiCodePath)
        {
            _dumpCsPath = dumpCsPath;
            _apiCodePath = apiCodePath;
        }

        public DatabaseDiff CreateDiff()
        {
            var result = new DatabaseDiff();

            CreateTableDiff(result.TableDiff);
            CreateEntityMDiff(result.EntityDiff);

            return result;
        }

        #region TableElement difference

        private void CreateEntityMDiff(IList<EntityDiff> entityDiff)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(_apiCodePath, TablePath_), "EntityM*.cs"))
            {
                var ownFields = ParseOwnEntity(file, out var entityMName).ToArray();
                var dumpFields = ParseDumpEntity(entityMName).ToArray();

                var ownDict = ownFields.ToDictionary(x => x.Name, y => y);
                var dumpDict = dumpFields.ToDictionary(x => x.Name, y => y);

                var diff = new EntityDiff { Name = entityMName };

                // Determine new and changed tables
                foreach (var dumpField in dumpFields)
                    if (!ownDict.ContainsKey(dumpField.Name))
                        diff.FieldDiff.Add(new EntityFieldDiff { Type = DiffType.New, DumpFieldInfo = dumpField });
                    else if (dumpField.Offset != ownDict[dumpField.Name].Offset)
                        diff.FieldDiff.Add(new EntityFieldDiff { Type = DiffType.Changed, DumpFieldInfo = dumpField, OwnFieldInfo = ownDict[dumpField.Name] });

                // Determine removed tables
                foreach (var ownField in ownFields)
                    if (!dumpDict.ContainsKey(ownField.Name))
                        diff.FieldDiff.Add(new EntityFieldDiff { Type = DiffType.Removed, OwnFieldInfo = dumpDict[ownField.Name] });

                if (diff.FieldDiff.Count > 0)
                    entityDiff.Add(diff);
            }
        }

        private IList<FieldInfo> ParseOwnEntity(string file, out string entityMName)
        {
            var ownNode = ParserSupport.ParseFile(file);

            entityMName = ownNode.FirstOrDefault(SyntaxNodeKind.NamespaceUnit)
                .FirstOrDefault(SyntaxNodeKind.Class).FirstOrDefault(SyntaxNodeKind.ClassStatement)
                .Where(SyntaxNodeKind.Identifier)[1].Text;

            var ownEntityFieldList = new List<FieldInfo>();

            var classNodes = ownNode.FirstOrDefault(SyntaxNodeKind.NamespaceUnit).FirstOrDefault(SyntaxNodeKind.Class).FirstOrDefault(SyntaxNodeKind.ClassBlock).Nodes;
            for (var i = 0; i < classNodes.Count; i++)
            {
                if (classNodes[i].Kind != SyntaxNodeKind.Property)
                    continue;

                var property = classNodes[i];
                var offsetComment = classNodes[i + 1];

                ownEntityFieldList.Add(new FieldInfo
                {
                    Offset = int.Parse(offsetComment.FirstOrDefault(SyntaxNodeKind.CommentText).Text.TrimEnd()[2..], NumberStyles.HexNumber),
                    Type = string.Join("", property.FirstOrDefault(SyntaxNodeKind.Type).FirstOrDefault(SyntaxNodeKind.Fqdn).Nodes),
                    Name = property.FirstOrDefault(SyntaxNodeKind.MemberName).FirstOrDefault(SyntaxNodeKind.Identifier).Text
                });
            }

            return ownEntityFieldList.ToArray();
        }

        private IEnumerable<FieldInfo> ParseDumpEntity(string entityMName)
        {
            var classLine = $"public class {entityMName}";
            var node = ParserSupport.ParseClass(_dumpCsPath, ParserSupport.FindIndexByString(classLine));

            var nodes = node.FirstOrDefault(SyntaxNodeKind.ClassBlock).Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Kind != SyntaxNodeKind.Field)
                    continue;

                var fieldName = string.Join("", nodes[i].FirstOrDefault(SyntaxNodeKind.Type).FirstOrDefault(SyntaxNodeKind.GenericTypes).FirstOrDefault(SyntaxNodeKind.Type).FirstOrDefault(SyntaxNodeKind.Fqdn).Nodes.Select(x => x.Text));
                var offsetComment = nodes[i + 1];

                yield return new FieldInfo
                {
                    Offset = int.Parse(offsetComment.FirstOrDefault(SyntaxNodeKind.CommentText).Text.Trim()[2..], NumberStyles.HexNumber),
                    Type = null,    // TODO: Set correct type
                    Name = fieldName
                };
            }
        }

        #endregion

        #region Table difference

        private void CreateTableDiff(IList<TableDiff> tableDiff)
        {
            var dumpFields = ParseDumpDarkMasterDb(_dumpCsPath).ToArray();
            var ownFields = ParseOwnDarkMasterDb(Path.Combine(_apiCodePath, MasterDatabasePath_)).ToArray();

            var ownDict = ownFields.ToDictionary(x => x.Name, y => y);
            var dumpDict = dumpFields.ToDictionary(x => x.Name, y => y);

            // Determine new and changed tables
            foreach (var dumpField in dumpFields)
                if (!ownDict.ContainsKey(dumpField.Name))
                    tableDiff.Add(new TableDiff { Type = DiffType.New, DumpFieldInfo = dumpField });
                else if (dumpField.Offset != ownDict[dumpField.Name].Offset)
                    tableDiff.Add(new TableDiff { Type = DiffType.Changed, DumpFieldInfo = dumpField, OwnFieldInfo = ownDict[dumpField.Name] });

            // Determine removed tables
            foreach (var ownField in ownFields)
                if (!dumpDict.ContainsKey(ownField.Name))
                    tableDiff.Add(new TableDiff { Type = DiffType.Removed, OwnFieldInfo = dumpDict[ownField.Name] });
        }

        private IEnumerable<FieldInfo> ParseDumpDarkMasterDb(string dumpPath)
        {
            var masterDbLine = "public sealed class DarkMasterMemoryDatabase";
            var node = ParserSupport.ParseClass(dumpPath, ParserSupport.FindIndexByString(masterDbLine));

            var offset = 0x10;
            foreach (var field in node.FirstOrDefault(SyntaxNodeKind.ClassBlock).Nodes.Where(x => x.Kind == SyntaxNodeKind.Field))
            {
                var text = string.Join("", field.FirstOrDefault(SyntaxNodeKind.Type).FirstOrDefault(SyntaxNodeKind.Fqdn).Nodes.Select(x => x.Text));
                yield return new FieldInfo
                {
                    Offset = offset,
                    Type = text,
                    Name = text
                };
                offset += 8;
            }
        }

        private IEnumerable<FieldInfo> ParseOwnDarkMasterDb(string masterPath)
        {
            var node = ParserSupport.ParseFile(masterPath);

            var classNodes = node.FirstOrDefault(SyntaxNodeKind.NamespaceUnit).FirstOrDefault(SyntaxNodeKind.Class).FirstOrDefault(SyntaxNodeKind.ClassBlock).Nodes;
            for (var i = 0; i < classNodes.Count; i++)
            {
                if (classNodes[i].Kind != SyntaxNodeKind.Property)
                    continue;

                var offsetComment = classNodes[i - 1];
                var property = classNodes[i];

                yield return new FieldInfo
                {
                    Offset = int.Parse(offsetComment.FirstOrDefault(SyntaxNodeKind.CommentText).Text.Trim()[2..], NumberStyles.HexNumber),
                    Type = string.Join("", property.FirstOrDefault(SyntaxNodeKind.Type).FirstOrDefault(SyntaxNodeKind.Fqdn).Nodes),
                    Name = property.FirstOrDefault(SyntaxNodeKind.MemberName).FirstOrDefault(SyntaxNodeKind.Identifier).Text
                };
            }
        }

        #endregion
    }
}
