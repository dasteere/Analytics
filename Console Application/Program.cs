using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Google.Apis.Analytics.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Timer = System.Windows.Forms.Timer;


namespace Console_Application
{
    public class UserLoc
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Count { get; set; }
    }

    public class CityInfo
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }


    public class Program
    {
        public static void Main(string[] args)
        {
            var timeRt = new System.Timers.Timer(30000);
            var u = new Updater();
            u.updateRt(null, null);
            timeRt.Elapsed += new ElapsedEventHandler(u.updateRt);
            timeRt.Interval = 30000;
            timeRt.Start();
            while (true)
            {
                Console.ReadKey(true);
            }
        }
        



    }

    public class Updater
    {
        private List<UserLoc> prevCurUsers = null;
        private List<UserLoc> curUsers = null;
        public void updateRt(object source, ElapsedEventArgs e)
        {
            getRtData();
        }
        private void deleteAll(string db)
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
                Console.Write("ERROR in RtService delete: " + e);
            }
        }
        private void insert(List<UserLoc> locs, string db)
        {
            try
            {
                SqlConnection conn =
                    new SqlConnection(
                        "Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False");
                DataSet ds = ToDataSet(locs);
                var sourceData = ds.Tables[0];
                conn.Open();
                using (SqlBulkCopy bulkCopy =
                    new SqlBulkCopy(conn.ConnectionString))
                {
                    // column mappings
                    bulkCopy.ColumnMappings.Add("Latitude", "Latitude");
                    bulkCopy.ColumnMappings.Add("Longitude", "Longitude");
                    bulkCopy.ColumnMappings.Add("Count", "Count");
                    bulkCopy.DestinationTableName = db;
                    bulkCopy.WriteToServer(sourceData);
                }
                conn.Close();
            }
            catch (Exception e)
            {
                Console.Write("ERROR in RtService insert method: " + e);
            }
            
        }
        private static DataSet ToDataSet<T>(IList<T> list)
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

        public async void getRtData()
        {
            try
            {
                UserCredential credential;
                using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
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
                var request = service.Data.Realtime.Get("ga:" + 67281419, "rt:activeUsers");
                request.Dimensions = "rt:latitude,rt:longitude";
                request.MaxResults = 10000;
                var feed = request.Execute();
                var listUsers = new List<UserLoc>();
                foreach (var list in feed.Rows)
                {
                    var row = (List<string>)list;
                    var u = new UserLoc
                    {
                        Latitude = Double.Parse(row[0]),
                        Longitude = Double.Parse(row[1]),
                        Count = Int32.Parse(row[2])
                    };
                    if (u.Latitude != 0 && u.Longitude != 0)
                        listUsers.Add(u);
                }
                this.prevCurUsers = this.curUsers;
                this.curUsers = listUsers;
                if (this.prevCurUsers != null)
                    updateChange();
            }
            catch (Exception e)
            {
                Console.Write("ERROR in RtService getRtData: " + e.ToString());
            }
            
        }
        public void updateChange()
        {
            try
            {
                var threshold = 1;
                var locs = new List<UserLoc>();
                foreach (var u in this.curUsers)
                {
                    var u2 = prevCurUsers.Find(x => x.Latitude == u.Latitude && x.Longitude == u.Longitude);
                    if (u2 == null && u.Count >= threshold)
                    {
                        if (u.Latitude != 0 && u.Longitude != 0)
                        {
                            locs.Add(u);
                        }
                    }
                    else if (u2 != null && u.Count - u2.Count >= threshold)
                    {
                        locs.Add(new UserLoc
                        {
                            Latitude = u.Latitude,
                            Longitude = u.Longitude,
                            Count = u.Count - u2.Count
                        });
                    }
                }
                deleteAll("RecentChange");
                insert(locs, "RecentChange");
            }
            catch (Exception e)
            {
                Console.Write("ERROR in RtService updateChange: " + e.ToString());
            } 
        }
    }
}

