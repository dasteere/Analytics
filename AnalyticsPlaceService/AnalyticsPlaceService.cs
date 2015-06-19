using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using Google.Apis.Analytics.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace AnalyticsPlaceService
{
    public partial class AnalyticsPlaceService : ServiceBase
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        public AnalyticsPlaceService(string[] args)
        {
            InitializeComponent();
            string eventSourceName = "MySource";
            string logName = "MyNewLog";
            if (args.Count() > 0)
            {
                eventSourceName = args[0];
            }
            if (args.Count() > 1)
            {
                logName = args[1];
            }
            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists(eventSourceName))
            {
                System.Diagnostics.EventLog.CreateEventSource(eventSourceName, logName);
            }
            eventLog1.Source = eventSourceName;
            eventLog1.Log = logName;
        }

        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
            eventLog1.WriteEntry("Test this stuff");
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = 6000; // 60 seconds
            var u = new Updater();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimer);
            timer.Start();
            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.
            var u = new Updater();
            u.updatePlaces(eventLog1, 0);
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("In onStop.");
        }

        protected override void OnContinue()
        {
            eventLog1.WriteEntry("In OnContinue.");
        }

        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public long dwServiceType;
            public ServiceState dwCurrentState;
            public long dwControlsAccepted;
            public long dwWin32ExitCode;
            public long dwServiceSpecificExitCode;
            public long dwCheckPoint;
            public long dwWaitHint;
        };
    }
    public class CityInfo
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }
    public class Updater
    {

        public EventLog log;
        public void updatePlaces(EventLog u, int eventId)
        {
            this.log = u;
            updateCitiesDB();
            updateCountriesDB();
        }
        public void deleteAll(string db)
        {
            try
            {
                SqlConnection conn = new SqlConnection();
                conn.ConnectionString =
                "Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False";
                conn.Open();
                var cmd = new SqlCommand("TRUNCATE TABLE " + db, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in PlaceService delete: " + e);
            }
            
        }
        public static DataSet ToDataSet<T>(IList<T> list)
        {
            Type elementType = typeof(T);
            DataSet ds = new DataSet();
            DataTable t = new DataTable();
            ds.Tables.Add(t);
            //add a column to table for each public property on T
            foreach (var propInfo in elementType.GetProperties())
            {
                Type ColType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

                t.Columns.Add(propInfo.Name, ColType);
            }

            //go through each property on T and add each value to the table
            foreach (T item in list)
            {
                DataRow row = t.NewRow();

                foreach (var propInfo in elementType.GetProperties())
                {
                    row[propInfo.Name] = propInfo.GetValue(item, null) ?? DBNull.Value;
                }

                t.Rows.Add(row);
            }

            return ds;
        }
        public void insertPlaces(List<CityInfo> places, string db)
        {
            try
            {
                SqlConnection conn = new SqlConnection("Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False");
                DataSet ds = ToDataSet(places);
                var sourceData = ds.Tables[0];
                conn.Open();
                using (SqlBulkCopy bulkCopy =
                            new SqlBulkCopy(conn.ConnectionString))
                {
                    // column mappings
                    bulkCopy.ColumnMappings.Add("Name", "Name");
                    bulkCopy.ColumnMappings.Add("Count", "Count");
                    bulkCopy.DestinationTableName = db;
                    bulkCopy.WriteToServer(sourceData);
                }
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in PlaceService insert: " + e);
            }
            
            /*SqlTransaction transaction = conn.BeginTransaction("test");
            SqlCommand cmd = conn.CreateCommand();
            cmd.Connection = conn;
            cmd.Transaction = transaction;
            var id = 0;
            foreach (var u in places)
            {
                string saveStaff = "INSERT into " + db + " (Name,Count) " + " VALUES ('" + u.Name + "', '" + u.Count + "');";
                cmd.CommandText = saveStaff;
                if (u.Name.Contains("'"))
                {
                    saveStaff = "INSERT into " + db + " (Name,Count) " + " VALUES ('@Name" + id + "', '" + u.Count +
                                "');";
                    cmd.CommandText = saveStaff;
                    cmd.Parameters.AddWithValue("@Name" + id, u.Name);
                    id++;
                }
                //cmd.Parameters.AddWithValue("@Name", u.Name);
                cmd.ExecuteNonQuery();
            }
            transaction.Commit();*/
        }
        public async void updateCitiesDB()
        {
            UserCredential credential;
            using (var stream = new FileStream("C:\\Users\\asteere\\Documents\\Visual Studio 2015\\Projects\\Google Analytics 2.0\\AnalyticsPlaceService\\client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { AnalyticsService.Scope.Analytics },
                    "user", CancellationToken.None, new FileDataStore("Books.ListMyLibrary"));
            }
            // Create the service.
            var service = new AnalyticsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Analytics API Sample",
            });
            DateTime t = DateTime.Today;
            var request2 = service.Data.Realtime.Get("ga:" + 67281419, "rt:activeUsers");
            request2.Dimensions = "rt:city";
            request2.Sort = "-rt:activeUsers";
            request2.MaxResults = 6;
            var topCities = new List<CityInfo>();
            try
            {
                var gaFeed = request2.Execute();
                foreach (var list in gaFeed.Rows)
                {
                    var row = (List<string>)list;
                    var u = new CityInfo
                    {
                        Name = row[0],
                        Count = Int32.Parse(row[1])
                    };
                    if (!u.Name.Contains("zz"))
                        topCities.Add(u);
                }
                deleteAll("TopCities");
                insertPlaces(topCities, "TopCities");
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in PlaceService updateCities: " + e);
            }
        }
        public async void updateCountriesDB()
        {
            UserCredential credential;
            using (var stream = new FileStream("C:\\Users\\asteere\\Documents\\Visual Studio 2015\\Projects\\Google Analytics 2.0\\AnalyticsPlaceService\\client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { AnalyticsService.Scope.Analytics },
                    "user", CancellationToken.None, new FileDataStore("Books.ListMyLibrary"));
            }
            // Create the service.
            var service = new AnalyticsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Analytics API Sample",
            });
            DateTime t = DateTime.Today;
            var request2 = service.Data.Realtime.Get("ga:" + 67281419, "rt:activeUsers");
            request2.Dimensions = "rt:country";
            request2.Sort = "-rt:activeUsers";
            request2.MaxResults = 5;
            var topCountries = new List<CityInfo>();
            try
            {
                var gaFeed = request2.Execute();
                foreach (var list in gaFeed.Rows)
                {
                    var row = (List<string>)list;
                    var u = new CityInfo
                    {
                        Name = row[0],
                        Count = Int32.Parse(row[1])
                    };
                    topCountries.Add(u);
                }
                deleteAll("TopCountries");
                insertPlaces(topCountries, "TopCountries");
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in PlaceService updateCountries: " + e.ToString());
            }
        }

    }
}
