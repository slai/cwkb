using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace cwkb
{
    public abstract class CWObject
    {
        public int Id { get; set; }
        
        protected static void FillFromDataReader<T>(T o, Dictionary<string, Action<T, object>> fieldMap, System.Data.IDataReader dataReader)
        {
            for (var i = 0; i < dataReader.FieldCount; i++)
            {
                var fieldName = dataReader.GetName(i);
                var value = dataReader.GetValue(i);
                if (value is DBNull) value = null;

                if (fieldMap.Keys.Contains(fieldName))
                    fieldMap[fieldName](o, value);
            }
        }
    }

    public abstract class CWEntryObject : CWObject
    {
        public string Excerpt { get; protected set; }

        public abstract void SetExcerpt(List<string> words, int length = 300);
    }

    public class TimeEntry : CWEntryObject
    {
        public string Member { get; set; }
        public string Notes { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string InternalNotes { get; set; }
        public decimal ActualHours { get; set; }
        public decimal BilledHours { get; set; }
        public string Status { get; set; }
        public Ticket Ticket { get; private set; }
        public TicketDBReference TicketDBReference { get; set; }
        public bool TicketLoaded { get; private set; }
        
        protected static Dictionary<string, Action<TimeEntry, object>> fieldMap = new Dictionary<string,Action<TimeEntry,object>>()
            {
                { "Time_RecID", (e, v) => e.Id = (int)v },
                { "Member_ID", (e, v) => e.Member = (string)v },
                { "Notes", (e, v) => e.Notes = (string)v },
                { "Time_Start", (e, v) => {
                    var d = (DateTime)v;
                    if (e.StartTime != null)
                        e.StartTime = new DateTime(e.StartTime.Year, e.StartTime.Month, e.StartTime.Day, d.Hour, d.Minute, d.Second);
                    else
                        e.StartTime = d;
                }},
                { "Time_End", (e, v) => {
                    var d = (DateTime)v;
                    if (e.EndTime != null)
                        e.EndTime = new DateTime(e.EndTime.Year, e.EndTime.Month, e.EndTime.Day, d.Hour, d.Minute, d.Second);
                    else
                        e.EndTime = d;
                }},
                { "Date_Start", (e, v) => {
                    var d = (DateTime)v;
                    if (e.StartTime != null)
                        e.StartTime = new DateTime(d.Year, d.Month, d.Day, e.StartTime.Hour, e.StartTime.Minute, e.StartTime.Second);
                    else
                        e.StartTime = d;

                    if (e.EndTime != null)
                        e.EndTime = new DateTime(d.Year, d.Month, d.Day, e.EndTime.Hour, e.EndTime.Minute, e.EndTime.Second);
                    else
                        e.EndTime = d;
                }},
                { "Internal_Note", (e, v) => e.InternalNotes = (string)v },
                { "Hours_Actual", (e, v) => e.ActualHours = (decimal)v },
                { "Hours_Bill", (e, v) => e.BilledHours = (decimal)v },
                { "__Status", (e, v) => e.Status = (string)v },
                { "SR_Service_RecID", (e, v) => e.TicketDBReference = (v == null ? null : new TicketDBReference((int)v)) },
            };

        public TimeEntry()
        {
            // TODO: init values to default
        }

        public TimeEntry(System.Data.IDataReader dataReader)
            : base()
        {
            FillFromDataReader<TimeEntry>(this, fieldMap, dataReader);
        }

        public void LoadTicket(Ticket ticket)
        {
            Ticket = ticket;
            TicketLoaded = true;
        }

        public override void SetExcerpt(List<string> words, int length = 300)
        {
            var excerptFields = new List<string>();
            excerptFields.Add(Notes);
            excerptFields.Add(InternalNotes);

            Excerpt = SearchUtil.GetExcerpt(words, excerptFields, excerptFields[0], length);
        }
    }

    // This is a base class for tickets.
    public abstract class Ticket : CWEntryObject
    {
        public string Name { get; set; }
        public string CompanyName { get; set; }
        public string Member { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? ClosedTime { get; set; }
        public DateTime LastUpdated { get; set; }
        public string LastUpdatedBy { get; set; }
        public string Status { get; set; }
        public List<TimeEntry> TimeEntries { get; private set; }
        public bool TimeEntriesLoaded { get; private set; }
        public List<TicketNote> Notes { get; private set; }
        public bool NotesLoaded { get; private set; }

        public void LoadTimeEntries(List<TimeEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException("entries");

            TimeEntries = entries;
            TimeEntriesLoaded = true;

            // link every time entry back to this ticket
            foreach (var e in entries)
                if (!e.TicketLoaded)
                    e.LoadTicket(this);
        }

        public void LoadNotes(List<TicketNote> entries)
        {
            if (entries == null)
                throw new ArgumentNullException("entries");

            Notes = entries;
            NotesLoaded = true;
        }

        public override void SetExcerpt(List<string> words, int length = 300)
        {
            var excerptFields = new List<string>();
            excerptFields.Add(Problem);
            excerptFields.Add(InternalAnalysis);
            excerptFields.Add(Resolution);

            Excerpt = SearchUtil.GetExcerpt(words, excerptFields, excerptFields[0], length);
        }

        public List<TicketNote> ProblemNotes
        {
            get
            {
                if (!NotesLoaded) return new List<TicketNote>();

                var notes = from n in Notes where n.Type == TicketNoteType.Problem select n;
                return notes.ToList();
            }
        }

        public string Problem
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (var n in ProblemNotes)
                    sb.Append(String.Format("*** {0} at {1} ***\n{2}\n", n.LastUpdatedBy, n.LastUpdated, n.Note));

                return sb.ToString();
            }
        }

        public List<TicketNote> InternalAnalysisNotes
        {
            get
            {
                if (!NotesLoaded) return new List<TicketNote>();

                var notes = from n in Notes where n.Type == TicketNoteType.InternalAnalysis select n;
                return notes.ToList();
            }
        }

        public string InternalAnalysis
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (var n in InternalAnalysisNotes)
                    sb.Append(String.Format("*** {0} at {1} ***\n{2}\n", n.LastUpdatedBy, n.LastUpdated, n.Note));

                return sb.ToString();
            }
        }

        public List<TicketNote> ResolutionNotes
        {
            get
            {
                if (!NotesLoaded) return new List<TicketNote>();

                var notes = from n in Notes where n.Type == TicketNoteType.Resolution select n;
                return notes.ToList();
            }
        }

        public string Resolution
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (var n in ResolutionNotes)
                    sb.Append(String.Format("*** {0} at {1} ***\n{2}\n", n.LastUpdatedBy, n.LastUpdated, n.Note));

                return sb.ToString();
            }
        }
    }

    public class TicketNote : CWEntryObject
    {
        public string Note { get; set; }
        public TicketNoteType Type { get; set; }
        public string Member { get; set; }
        public DateTime LastUpdated { get; set; }
        public string LastUpdatedBy { get; set; }

        protected static Dictionary<string, Action<TicketNote, object>> fieldMap = new Dictionary<string, Action<TicketNote, object>>()
            {
                { "SR_Detail_RecID", (e, v) => e.Id = (int)v },
                { "Created_By", (e, v) => e.Member = (string)v },
                { "SR_Detail_Notes", (e, v) => e.Note = (string)v },
                { "Last_Update", (e, v) => e.LastUpdated = (DateTime)v },
                { "Updated_By", (e, v) => e.LastUpdatedBy = (string)v },
                { "Problem_Flag", (e, v) => e.Type = (bool)v ? TicketNoteType.Problem : e.Type },
                { "InternalAnalysis_Flag", (e, v) => e.Type = (bool)v ? TicketNoteType.InternalAnalysis : e.Type },
                { "Resolution_Flag", (e, v) => e.Type = (bool)v ? TicketNoteType.Resolution : e.Type },
            };

        public TicketNote()
        {
            // TODO: fill in with default values.
        }

        public TicketNote(System.Data.IDataReader dataReader)
            : base()
        {
            FillFromDataReader<TicketNote>(this, fieldMap, dataReader);
        }

        public override void SetExcerpt(List<string> words, int length = 300)
        {
            var excerptFields = new List<string>();
            excerptFields.Add(Note);

            Excerpt = SearchUtil.GetExcerpt(words, excerptFields, excerptFields[0], length);
        }
    }

    public enum TicketNoteType 
    {
        NotSet = 0,
        Problem,
        InternalAnalysis,
        Resolution
    }

    public class ProjectTicket : Ticket
    {
        public string PhaseDesc { get; set; }
        public string ProjectName { get; set; }

        protected static Dictionary<string, Action<ProjectTicket, object>> fieldMap = new Dictionary<string, Action<ProjectTicket, object>>()
            {
                { "SR_Service_RecID", (e, v) => e.Id = (int)v },
                { "Entered_By", (e, v) => e.Member = (string)v },
                { "Summary", (e, v) => e.Name = (string)v },
                { "Date_Entered", (e, v) => e.StartTime = (DateTime)v },
                { "Date_Closed", (e, v) => e.ClosedTime = (v == null ? e.ClosedTime : (DateTime)v) },
                { "Last_Update", (e, v) => e.LastUpdated = (DateTime)v },
                { "Updated_By", (e, v) => e.LastUpdatedBy = (string)v },
                { "__Company_Name", (e, v) => e.CompanyName = (string)v },
                { "__Status", (e, v) => e.Status = (string)v },
                { "__Phase_Desc", (e, v) => e.PhaseDesc = (string)v },
                { "__Project_Name", (e, v) => e.ProjectName = (string)v },
            };

        public ProjectTicket()
        {
            // TODO: fill in with default values.
        }

        public ProjectTicket(System.Data.IDataReader dataReader)
            : base()
        {
            FillFromDataReader<ProjectTicket>(this, fieldMap, dataReader);
        }
    }

    public class ServiceTicket : Ticket
    {
        protected static Dictionary<string, Action<ServiceTicket, object>> fieldMap = new Dictionary<string, Action<ServiceTicket, object>>()
            {
                { "SR_Service_RecID", (e, v) => e.Id = (int)v },
                { "Entered_By", (e, v) => e.Member = (string)v },
                { "Summary", (e, v) => e.Name = (string)v },
                { "Date_Entered", (e, v) => e.StartTime = (DateTime)v },
                { "Date_Closed", (e, v) => e.ClosedTime = (v == null ? e.ClosedTime : (DateTime)v) },
                { "Last_Update", (e, v) => e.LastUpdated = (DateTime)v },
                { "Updated_By", (e, v) => e.LastUpdatedBy = (string)v },
                { "__Company_Name", (e, v) => e.CompanyName = (string)v },
                { "__Status", (e, v) => e.Status = (string)v },
            };

        public ServiceTicket()
        {
            // TODO: fill in with default values.
        }

        public ServiceTicket(System.Data.IDataReader dataReader)
            : base()
        {
            FillFromDataReader<ServiceTicket>(this, fieldMap, dataReader);
        }
    }

    public class TicketDBReference
    {
        public int Id { get; set; }

        public TicketDBReference(int id)
        {
            Id = id;
        }
    }

    public class KBArticle : CWEntryObject
    {
        public string Summary { get; set; }
        public string Problem { get; set; }
        public string Resolution { get; set; }
        public DateTime LastUpdated { get; set; }
        public string LastUpdatedBy { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }

        protected static Dictionary<string, Action<KBArticle, object>> fieldMap = new Dictionary<string, Action<KBArticle, object>>()
            {
                { "KB_Resolution_RecID", (e, v) => e.Id = (int)v },
                { "Summary", (e, v) => e.Summary = (string)v },
                { "Detail_Problem", (e, v) => e.Problem = (string)v },
                { "Resolution", (e, v) => e.Resolution = (string)v },
                { "Last_Update", (e, v) => e.LastUpdated = (DateTime)v },
                { "Updated_By", (e, v) => e.LastUpdatedBy = (string)v },
                { "__CategoryDesc", (e, v) => e.Category = v == null ? e.Category : (string)v },
                { "__SubCategoryDesc", (e, v) => e.SubCategory = v == null ? e.SubCategory : (string)v },
            };

        public KBArticle()
        {
            // TODO: fill in with default values.
        }

        public KBArticle(System.Data.IDataReader dataReader)
            : base()
        {
            FillFromDataReader<KBArticle>(this, fieldMap, dataReader);
        }

        public override void SetExcerpt(List<string> words, int length = 300)
        {
            var excerptFields = new List<string>();
            excerptFields.Add(Summary);
            excerptFields.Add(Problem);
            excerptFields.Add(Resolution);

            Excerpt = SearchUtil.GetExcerpt(words, excerptFields, excerptFields[0], length);
        }
    }

    public class FtsResult : CWObject
    {
        public string SourceTable { get; set; }
        public int Rank { get; set; }
        public CWEntryObject Item { get; private set; }
        public bool ItemLoaded { get; private set; }

        protected static Dictionary<string, Action<FtsResult, object>> fieldMap = new Dictionary<string, Action<FtsResult, object>>()
            {
                { "Source_Table", (e, v) => e.SourceTable = (string)v },
                { "ResultRowID", (e, v) => e.Id = (int)v },
                { "Rank", (e, v) => e.Rank = (int)v }
            };

        public FtsResult(System.Data.IDataReader dataReader)
        {
            FillFromDataReader<FtsResult>(this, fieldMap, dataReader);
        }

        public void LoadItem(CWEntryObject item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            Item = item;
            ItemLoaded = true;
        }
    }

    public class FTSCatalogStatus
    {
        public string Catalog { get; set; }
        public int IndexSize { get; set; } // in MB
        public int ItemCount { get; set; }
        public DateTime LastPopulated { get; set; }
    }

    public class FTSTableStatus
    {
        public string Table { get; set; }
        public bool IndexingEnabled { get; set; }
        public bool ChangeTrackingEnabled { get; set; }
        public bool BackgroundUpdateEnabled { get; set; }
        public int DocsProcessed { get; set; }
        public int ItemCount { get; set; }
        public int PendingChangesCount { get; set; }
        public int FailedCount { get; set; }
        public FTSPopulateStatus PopulateStatus { get; set; }
    }

    public enum FTSPopulateStatus
    {
        Idle = 0,
        FullPopulation = 1,
        IncrementalPopulation = 2,
        PropagatingTrackedChanges = 3,
        BackgroundIndexUpdate = 4,
        ThrottledPaused = 5
    }
}