using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace GlobalSettingsManager
{
    public class SqlRepository : ISettingsRepository
    {
        private const string MergeQueryTemplate = @"MERGE INTO {0} AS target
                USING (VALUES (@name, @value, @category )) AS source (Name, Value, Category)
	                ON target.Name = source.Name AND target.Category = source.Category
                WHEN MATCHED AND ISNULL(target.Value,'') <> ISNULL(source.Value,'') AND target.UpdatedAt < GETUTCDATE() THEN
	                UPDATE SET target.Value=@value, target.UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED THEN
	                INSERT(Category, Name, Value, UpdatedAt)
	                VALUES(source.Category, source.Name, source.Value, GETUTCDATE());";
        
        
        private readonly string _connectionString;
        private readonly string _settingsTableName;
        private readonly string _mergeQuery;

        /// <summary>
        /// Default is false; If set - prevents repository from altering database
        /// </summary>
        public bool ReadOnly { get; set; }


        /// <param name="connectionString">Connection string for settings database</param>
        /// <param name="settingsTableName">Settings table name. Will be used in queries tamplates like this: "FROM {0} WHERE"</param>
        public SqlRepository(string connectionString, string settingsTableName)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Ivalid connection string");
            if (string.IsNullOrEmpty(settingsTableName))
                throw new ArgumentException("Ivalid settings table name");
            _connectionString = connectionString;
            _settingsTableName = settingsTableName;
            _mergeQuery = string.Format(MergeQueryTemplate, _settingsTableName);
        }

        public bool WriteSetting(SettingsStorageModel setting)
        {
            if (ReadOnly)
                return false;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(_mergeQuery, conn))
                {
                    if (setting.Value != null)
                        cmd.Parameters.Add("@value", SqlDbType.NVarChar).Value = setting.Value;
                    else
                        cmd.Parameters.Add("@value", SqlDbType.NVarChar).Value = DBNull.Value;
                    cmd.Parameters.Add("@name", System.Data.SqlDbType.VarChar).Value = setting.Name;
                    cmd.Parameters.Add("@category", System.Data.SqlDbType.VarChar).Value = setting.Category;
                    return cmd.ExecuteNonQuery() > 0;
                }
                
            }
        }

        public int WriteSettings(IEnumerable<SettingsStorageModel> settings)
        {
            if (ReadOnly)
                return 0;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var tran = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                var cnt = 0;

                foreach (var setting in settings)
                {
                    using (var cmd = new SqlCommand(_mergeQuery, conn))
                    {
                        cmd.Transaction = tran;
                        if (setting.Value != null)
                            cmd.Parameters.Add("@value", SqlDbType.NVarChar).Value = setting.Value;
                        else
                            cmd.Parameters.Add("@value", SqlDbType.NVarChar).Value = DBNull.Value;
                        cmd.Parameters.Add("@name", System.Data.SqlDbType.VarChar).Value = setting.Name;
                        cmd.Parameters.Add("@category", System.Data.SqlDbType.VarChar).Value = setting.Category;
                        cnt += cmd.ExecuteNonQuery();
                    }
                }
                tran.Commit();
                return cnt;
            }
        }

        public IEnumerable<SettingsStorageModel> ReadSettings(string category)
        {
            return ReadSettings(new List<string>(1) { category }, null);
        }

        public IEnumerable<SettingsStorageModel> ReadSettings(IList<string> categories, DateTime? lastChangedMin = null)
        {
            if (categories == null || categories.Count == 0)
                return null;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    if (lastChangedMin.HasValue)
                        cmd.Parameters.Add("@minTime", System.Data.SqlDbType.DateTime).Value = lastChangedMin;
                    var parameters = new string[categories.Count];
                    for (var i = 0; i < categories.Count; i++)
                    {
                        parameters[i] = string.Format("@cat{0}", i);
                        cmd.Parameters.AddWithValue(parameters[i], categories[i]);
                    }
                    var query = string.Format("SELECT category, name, value, UpdatedAt FROM {0} WHERE category in ({1})", _settingsTableName, string.Join(", ", parameters));
                    if (lastChangedMin.HasValue)
                        query += " AND UpdatedAt > @minTime";
                    cmd.CommandText = query;

                    using (var reader = cmd.ExecuteReader())
                    {
                        var result = new List<SettingsStorageModel>(42);
                        while (reader.Read())
                        {
                            var setting = new SettingsStorageModel()
                            {
                                Category = reader.GetString(0),
                                Name = reader.GetString(1),
                                Value = reader.IsDBNull(2) ? null : reader.GetString(2),
                                UpdatedAt = reader.GetDateTime(3)
                            };
                            result.Add(setting);
                        }
                        return result;
                    }
                }
            }
        }
    }
}