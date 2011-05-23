using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Data.SqlClient;

namespace cwkb
{
    public class DataAccess : IDisposable
    {
        private SqlConnection _conn;

        public DataAccess()
        {
            var config = WebConfigurationManager.OpenWebConfiguration(HttpContext.Current.Request.ApplicationPath);
			
            String connString = null;
            try
            {
                connString = config.ConnectionStrings.ConnectionStrings["CWDatabase"].ConnectionString;
            }
            catch (Exception e)
            {
                throw new Exception("No connection string to the CW database has been specified.", e);
            }

            _conn = new SqlConnection(connString);
            _conn.Open();
        }

        public TimeEntry GetTimeEntry(int id)
        {
            var entries = GetTimeEntries(new int[] { id });
            if (entries.Count > 0)
                return entries[0];
            else
                return null;
        }

        public List<TimeEntry> GetTimeEntries(int[] ids)
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;

            //http://stackoverflow.com/questions/337704/parameterizing-a-sql-in-clause/337792#337792
            var idsMap = new Dictionary<string, int>();
            for (var i = 0; i < ids.Length; i++)
                idsMap["@id" + i.ToString()] = ids[i];

            cmd.CommandText = @"SELECT Time_RecID, Member_ID, Notes, Date_Start, Time_Start, Time_End, Internal_Note, 
                                       Hours_Actual, Hours_Bill, TE_Status.Description AS __Status, SR_Service_RecID
                                FROM dbo.Time_Entry 
                                    INNER JOIN dbo.TE_Status ON TE_Status.TE_Status_ID = Time_Entry.TE_Status_ID
                                WHERE Time_RecID IN (" + string.Join(",", idsMap.Keys) + ")";
            
            foreach (var k in idsMap.Keys)
                cmd.Parameters.AddWithValue(k, idsMap[k]);

            var results = new List<TimeEntry>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    try
                    {
                        results.Add(new TimeEntry(reader));
                    }
                    catch (Exception ex)
                    {
                        // TODO: log
                    }
                }
            }

            // now get tickets
            var ticketIds = from e in results where e.TicketDBReference != null select e.TicketDBReference.Id;
            // some time entries don't have associated tickets, e.g. entered using charge code
            if (ticketIds.Count() > 0)
            {
                var tickets = GetTickets(ticketIds.ToArray());
                foreach (var e in results)
                {
                    if (e.TicketDBReference == null)
                        continue;

                    var ticket = tickets.FirstOrDefault(t => t.Id == e.TicketDBReference.Id);
                    if (ticket != null)
                        e.LoadTicket(ticket);
                }
            }

            return results;
        }

        public List<TimeEntry> GetTimeEntriesForTicket(int ticketId)
        {
            var entries = GetTimeEntriesForTickets(new int[] { ticketId });
            if (entries.Count > 0)
                return entries.SingleOrDefault().Value;
            else
                return null;
        }

        public Dictionary<int, List<TimeEntry>> GetTimeEntriesForTickets(int[] ticketIds)
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;

            //http://stackoverflow.com/questions/337704/parameterizing-a-sql-in-clause/337792#337792
            var idsMap = new Dictionary<string, int>();
            for (var i = 0; i < ticketIds.Length; i++)
                idsMap["@id" + i.ToString()] = ticketIds[i];

            cmd.CommandText = @"SELECT Time_RecID, Member_ID, Notes, Date_Start, Time_Start, Time_End, Internal_Note, 
                                       Hours_Actual, Hours_Bill, TE_Status.Description AS __Status, SR_Service_RecID
                                FROM dbo.Time_Entry 
                                    INNER JOIN dbo.TE_Status ON TE_Status.TE_Status_ID = Time_Entry.TE_Status_ID
                                WHERE SR_Service_RecID IN (" + string.Join(",", idsMap.Keys) + ")";

            foreach (var k in idsMap.Keys)
                cmd.Parameters.AddWithValue(k, idsMap[k]);

            var results = new Dictionary<int, List<TimeEntry>>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int ticketId = (int)reader["SR_Service_RecID"];
                    TimeEntry e = null;
                    try
                    {
                        e = new TimeEntry(reader);
                    }
                    catch (Exception ex)
                    {
                        // TODO: log
                    }

                    if (e != null)
                    {
                        List<TimeEntry> entries = null;
                        if (!results.ContainsKey(ticketId))
                        {
                            entries = new List<TimeEntry>();
                            results.Add(ticketId, entries);
                        }
                        else
                        {
                            entries = results[ticketId];
                        }

                        entries.Add(e);
                    }
                }
            }

            return results;
        }

        public Ticket GetTicket(int id, bool loadNotes = false, bool loadTimeEntries = false)
        {
            var entries = GetTickets(new int[] { id }, loadNotes, loadTimeEntries);
            if (entries.Count > 0)
                return entries[0];
            else
                return null;
        }

        public List<Ticket> GetTickets(int[] ids, bool loadNotes = false, bool loadTimeEntries = false)
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;

            //http://stackoverflow.com/questions/337704/parameterizing-a-sql-in-clause/337792#337792
            var idsMap = new Dictionary<string, int>();
            for (var i = 0; i < ids.Length; i++)
                idsMap["@id" + i.ToString()] = ids[i];

            cmd.CommandText = @"SELECT SR_Service.SR_Service_RecID, SR_Service.Entered_By, SR_Service.Summary, 
                                       SR_Service.Date_Entered, SR_Service.Date_Closed, 
                                       SR_Service.Last_Update, SR_Service.Updated_By, 
                                       Company.Company_Name AS __Company_Name, SR_Status.Description AS __Status,
                                       PM_Phase.Description AS __Phase_Desc, PM_Project.Project_ID AS __Project_Name 
                                FROM dbo.SR_Service 
                                    INNER JOIN dbo.SR_Status ON SR_Status.SR_Status_RecID = SR_Service.SR_Status_RecID
                                    INNER JOIN dbo.Company ON Company.Company_RecID = SR_Service.Company_RecID
                                    LEFT OUTER JOIN PM_Phase ON PM_Phase.PM_Phase_RecID = SR_Service.PM_Phase_RecID 
                                    LEFT OUTER JOIN PM_Project ON PM_Project.PM_Project_RecID = PM_Phase.PM_Project_RecID
                                WHERE SR_Service_RecID IN (" + string.Join(",", idsMap.Keys) + ")";

            foreach (var k in idsMap.Keys)
                cmd.Parameters.AddWithValue(k, idsMap[k]);

            var results = new List<Ticket>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    try
                    {
                        Ticket e = null;
                        // If __Project_Name is not null, then this ticket is a project ticket
                        if (!(reader["__Project_Name"] is DBNull))
                            e = new ProjectTicket(reader);
                        else
                            e = new ServiceTicket(reader);
                        results.Add(e);
                    }
                    catch (Exception ex)
                    {
                        // TODO: log
                    }
                }
            }

            // now get ticket notes
            if (loadNotes)
            {
                var ticketIds = from t in results select t.Id;
                var notes = GetTicketNotes(ticketIds.ToArray());
                foreach (var t in results)
                {
                    if (notes.ContainsKey(t.Id))
                        t.LoadNotes(notes[t.Id]);
                }
            }

            // now get ticket time entries
            if (loadTimeEntries)
            {
                var ticketIds = from t in results select t.Id;
                var entries = GetTimeEntriesForTickets(ticketIds.ToArray());
                foreach (var t in results)
                {
                    if (entries.ContainsKey(t.Id))
                        t.LoadTimeEntries(entries[t.Id]);
                }
            }

            return results;
        }

        public List<TicketNote> GetTicketNotes(int ticketId)
        {
            var entries = GetTicketNotes(new int[] { ticketId });
            if (entries.Count > 0)
                return entries[ticketId];
            else
                return new List<TicketNote>();
        }

        public Dictionary<int, List<TicketNote>> GetTicketNotes(int[] ticketIds)
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;

            //http://stackoverflow.com/questions/337704/parameterizing-a-sql-in-clause/337792#337792
            var idsMap = new Dictionary<string, int>();
            for (var i = 0; i < ticketIds.Length; i++)
                idsMap["@id" + i.ToString()] = ticketIds[i];

            cmd.CommandText = @"SELECT SR_Detail_RecID, Created_By, SR_Detail_Notes, Last_Update, Updated_By, 
                                    Problem_Flag, InternalAnalysis_Flag, Resolution_Flag, SR_Service_RecID
                                FROM dbo.SR_Detail 
                                WHERE SR_Service_RecID IN (" + string.Join(",", idsMap.Keys) + ")";

            foreach (var k in idsMap.Keys)
                cmd.Parameters.AddWithValue(k, idsMap[k]);

            var results = new Dictionary<int, List<TicketNote>>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int ticketId = (int)reader["SR_Service_RecID"];
                    TicketNote n = null;
                    try
                    {
                        n = new TicketNote(reader);
                    }
                    catch (Exception ex)
                    {
                        // TODO: log
                        continue;
                    }

                    if (n != null)
                    {
                        List<TicketNote> notes = null;
                        if (!results.ContainsKey(ticketId))
                        {
                            notes = new List<TicketNote>();
                            results.Add(ticketId, notes);
                        }
                        else
                        {
                            notes = results[ticketId];
                        }

                        notes.Add(n);
                    }
                }
            }

            return results;
        }

        public KBArticle GetKBArticle(int id)
        {
            var articles = GetKBArticles(new int[] { id });
            if (articles.Count > 0)
                return articles[0];
            else
                return null;
        }

        public List<KBArticle> GetKBArticles(int[] ids)
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;

            //http://stackoverflow.com/questions/337704/parameterizing-a-sql-in-clause/337792#337792
            var idsMap = new Dictionary<string, int>();
            for (var i = 0; i < ids.Length; i++)
                idsMap["@id" + i.ToString()] = ids[i];

            cmd.CommandText = @"SELECT KB_Resolution.KB_Resolution_RecID, KB_Resolution.Summary, KB_Resolution.Detail_Problem, 
                                       KB_Resolution.Resolution, KB_Resolution.Last_Update, KB_Resolution.Updated_By, 
                                       KB_Category.Description AS __CategoryDesc, 
                                       KB_SubCategory.Description AS __SubCategoryDesc
                                FROM KB_Resolution 
                                    LEFT OUTER JOIN KB_Category ON KB_Category.KB_Category_RecID = KB_Resolution.KB_Category_RecID 
                                    LEFT OUTER JOIN KB_SubCategory ON KB_SubCategory.KB_SubCategory_RecID = KB_Resolution.KB_SubCategory_RecID
                                WHERE KB_Resolution_RecID IN (" + string.Join(",", idsMap.Keys) + ")";

            foreach (var k in idsMap.Keys)
                cmd.Parameters.AddWithValue(k, idsMap[k]);

            var results = new List<KBArticle>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    try
                    {
                        results.Add(new KBArticle(reader));
                    }
                    catch (Exception ex)
                    {
                        // TODO: log
                    }
                }
            }

            return results;
        }

        public List<FtsResult> PerformSearch(string query, int maxResults = 51 /* +1 so we know if there are more than 50 results */)
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;

            cmd.CommandText = String.Format(
                              @"SELECT TOP {0} *
                                FROM (
	                                SELECT 'KB_Resolution' AS Source_Table, kbr.KB_Resolution_RecID AS ResultRowID, ftc.RANK
	                                FROM dbo.KB_Resolution AS kbr
		                                INNER JOIN CONTAINSTABLE (KB_Resolution, (Detail_Problem, Resolution, Summary), @query) AS ftc
			                                ON kbr.KB_Resolution_RecID = ftc.[KEY]
	                                UNION
	                                SELECT Source_Table, ResultRowID, MAX([RANK])
	                                FROM (
		                                -- GROUP BY eliminates detail records referring to the same ticket
		                                SELECT 'SR_Service' AS Source_Table, srd.SR_Service_RecID AS ResultRowID, MAX(ftc.RANK)
		                                FROM dbo.SR_Detail AS srd
			                                INNER JOIN CONTAINSTABLE(SR_Detail, SR_Detail_Notes, @query) AS ftc
				                                ON srd.SR_Detail_RecID = ftc.[KEY]
		                                GROUP BY srd.SR_Service_RecID
		                                UNION
		                                SELECT 'SR_Service' AS Source_Table, srs.SR_Service_RecID AS ResultRowID, ftc.RANK
		                                FROM dbo.SR_Service AS srs
			                                INNER JOIN CONTAINSTABLE(SR_Service, Summary, @query) AS ftc
				                                ON srs.SR_Service_RecID = ftc.[KEY]
	                                ) AS SR (Source_Table, ResultRowID, [RANK])
	                                GROUP BY Source_Table, ResultRowID
	                                UNION
	                                SELECT 'Time_Entry' AS Source_Table, te.Time_RecID AS ResultRowID, ftc.RANK
	                                FROM dbo.Time_Entry AS te 
		                                INNER JOIN CONTAINSTABLE (Time_Entry, Notes, @query) AS ftc 
			                                ON te.Time_RecID = ftc.[KEY]
                                ) AS UnionedResult
                                ORDER BY [RANK] DESC", maxResults);

            cmd.Parameters.AddWithValue("@maxResults", maxResults);
            cmd.Parameters.AddWithValue("@query", query);

            var ftsResults = new List<FtsResult>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    FtsResult r = null;
                    try
                    {
                        r = new FtsResult(reader);
                    }
                    catch (Exception ex)
                    {
                        // TODO: log
                    }

                    if (r != null)
                        ftsResults.Add(r);
                }
            }

            // populate results with items
            var ftsGrouped = (from r in ftsResults group r by r.SourceTable into g select new { Key = g.Key, Items = g.ToList() });
            foreach (var g in ftsGrouped)
            {
                var ids = (from i in g.Items select i.Id).ToArray();
                var tableMatches = g.Items;

                if (g.Key == "SR_Service")
                {
                    var tickets = GetTickets(ids, loadNotes: true);
                    foreach (var t in tickets)
                        g.Items.Single(i => i.Id == t.Id).LoadItem(t);
                }
                else if (g.Key == "Time_Entry")
                {
                    var entries = GetTimeEntries(ids);
                    foreach (var e in entries)
                        g.Items.Single(i => i.Id == e.Id).LoadItem(e);
                }
                else if (g.Key == "KB_Resolution")
                {
                    var articles = GetKBArticles(ids);
                    foreach (var e in articles)
                        g.Items.Single(i => i.Id == e.Id).LoadItem(e);
                }
                else
                {
                    // TODO: log. Should not happen.
                }
            }

            // eliminate results with no related item. Should not happen.
            ftsResults = (from r in ftsResults where r.Item != null select r).ToList();

            return ftsResults;
        }

        public DateTime? GetKBLastUpdateDate()
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;
            cmd.CommandText = "SELECT DATEADD(\"s\", (SELECT FULLTEXTCATALOGPROPERTY('Knowledgebase', N'PopulateCompletionAge')), '01/01/1990 00:00:00')";

            try
            {
                return (DateTime)cmd.ExecuteScalar();
            }
            catch (InvalidCastException e)
            {
                // this exception usually means that the user doesn't have the VIEW DEFINITION permission
                return null;
            }
        }

        public FTSCatalogStatus GetFTSCatalogStatus()
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;
            cmd.CommandText = @"SELECT FULLTEXTCATALOGPROPERTY('Knowledgebase', N'IndexSize');
                                SELECT FULLTEXTCATALOGPROPERTY('Knowledgebase', N'ItemCount');
                                SELECT DATEADD(""s"", (SELECT FulltextCatalogProperty('Knowledgebase', N'PopulateCompletionAge')), '01/01/1990 00:00:00');";
            
            var status = new FTSCatalogStatus();
            status.Catalog = "Knowledgebase";
    
            using (var reader = cmd.ExecuteReader())
            {
                try
                {
                    // must be read in order specified in SQL
                    reader.Read();
                    status.IndexSize = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.ItemCount = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.LastPopulated = reader.GetDateTime(0);
                }
                catch (System.Data.SqlTypes.SqlNullValueException ex)
                {
                    // this occurs usually if user doesn't have perms to view metadata
                    return null;
                }
            }

            return status;
        }

        public FTSTableStatus GetFTSTableStatus(string table)
        {
            var cmd = new SqlCommand();
            cmd.Connection = _conn;
            cmd.CommandText = @"SELECT OBJECTPROPERTYEX(OBJECT_ID(@table), N'TableFulltextBackgroundUpdateIndexOn');
                                SELECT OBJECTPROPERTYEX(OBJECT_ID(@table), N'TableFulltextChangeTrackingOn');
                                SELECT OBJECTPROPERTYEX(OBJECT_ID(@table), N'TableFulltextDocsProcessed');
                                SELECT OBJECTPROPERTYEX(OBJECT_ID(@table), N'TableFulltextFailCount');
                                SELECT OBJECTPROPERTYEX(OBJECT_ID(@table), N'TableFulltextItemCount');
                                SELECT OBJECTPROPERTYEX(OBJECT_ID(@table), N'TableFulltextPendingChanges');
                                SELECT OBJECTPROPERTYEX(OBJECT_ID(@table), N'TableFulltextPopulateStatus');
                                SELECT OBJECTPROPERTYEX(OBJECT_ID(@table), N'TableHasActiveFulltextIndex');";

            cmd.Parameters.AddWithValue("@table", table);

            var status = new FTSTableStatus();
            status.Table = table;

            using (var reader = cmd.ExecuteReader())
            {
                try
                {
                    // must be read in order specified in SQL
                    reader.Read();
                    status.BackgroundUpdateEnabled = reader.GetInt32(0) > 0; // 0 = false, 1 = true

                    reader.NextResult();
                    reader.Read();
                    status.ChangeTrackingEnabled = reader.GetInt32(0) > 0;

                    reader.NextResult();
                    reader.Read();
                    status.DocsProcessed = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.FailedCount = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.ItemCount = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.PendingChangesCount = reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.PopulateStatus = (FTSPopulateStatus)reader.GetInt32(0);

                    reader.NextResult();
                    reader.Read();
                    status.IndexingEnabled = reader.GetInt32(0) > 0;
                }
                catch (System.Data.SqlTypes.SqlNullValueException ex)
                {
                    // this occurs usually if user doesn't have perms to view metadata
                    return null;
                }
            }

            return status;
        }

        public void Dispose()
        {
            if (_conn != null)
            {
                _conn.Close();
                _conn = null;
            }
        }
    }
}