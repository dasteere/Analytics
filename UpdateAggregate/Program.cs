using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Google.Apis.Analytics.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace UpdateAggregate
{
    public class UserLoc
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Count { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            updateDay(null,null);
            var timeDay = new System.Timers.Timer(3600000);
            timeDay.Elapsed += new ElapsedEventHandler(updateDay);
            timeDay.Interval = 3600000;
            timeDay.Start();
            while (true)
            {
                Console.ReadKey(true);
            }
        }
        private static void updateDay(object source, ElapsedEventArgs e)
        {
            var u = new Updater();
            u.updateWorldDB();
            DateTime t = DateTime.Now;
            Console.Write("Updated at " + t.ToShortDateString() + " " + t.ToLongTimeString() + "\n");
        }

    }
    public class CityInfo
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    public class Updater
    {
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
                Console.Write("ERROR in AggregateService delete: " + e);
            }
        }
        public void insert(List<UserLoc> locs, string db)
        {
            try
            {
                SqlConnection conn = new SqlConnection("Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False");
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
            }
            catch (Exception e)
            {
                Console.Write("ERROR in AggregateService insert: " + e);
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

        public List<UserLoc> condenseLocs(List<UserLoc> places)
        {
            var store = new Dictionary<string, UserLoc>();
            foreach (var u in places)
            {
                if (store.ContainsKey(u.Latitude + "," + u.Longitude))
                {
                    store[u.Latitude + "," + u.Longitude] = new UserLoc
                    {
                        Latitude = u.Latitude,
                        Longitude = u.Longitude,
                        Count = store[u.Latitude + "," + u.Longitude].Count + u.Count
                    };
                }
                else
                {
                    store.Add(u.Latitude + "," + u.Longitude, u);
                }
            }
            return store.Values.ToList();
        }
        public IEnumerable<UserLoc> ReadFromDb(string db)
        {
            try
            {
                SqlConnection conn = new SqlConnection();
                conn.ConnectionString =
                    "Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False";
                conn.Open();
                string str = "SELECT * FROM " + db;
                var cmd = new SqlCommand(str, conn);
                List<UserLoc> persons = new List<UserLoc>();
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {

                    // Read advances to the next row.
                    //Console.Write("Updating row \n");
                    while (reader.Read())
                    {
                        persons.Add(new UserLoc
                        {
                            Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
                            Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
                            Count = reader.GetInt32(reader.GetOrdinal("Count"))
                        });
                    }
                }
                return persons;
            }
            catch (Exception e)
            {
                Console.Write("ERROR in AggregateService insertPlaces: " + e);
                return null;
            }
        }
        public async void updateWorldDB()
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
            DateTime t = DateTime.Now;
            var request2 = service.Data.Ga.Get("ga:" + 67281419, t.ToString("yyyy-MM-dd"), t.AddDays(1).ToString("yyyy-MM-dd"), "ga:users");
            request2.Dimensions = "ga:latitude,ga:longitude,ga:hour,ga:country";
            request2.Sort = "-ga:hour";
            request2.MaxResults = 10000;
            int hour = t.Hour - 2;
            if (hour < 0) hour += 24;
            string h = hour.ToString("00");
            request2.Filters = "ga:hour==" + h + ";ga:country!=United States;ga:country!=Canada;ga:country!=United Kingdom";
            var dayUsers = ReadFromDb("AggregateWorld").ToList();
            try
            {
                var gaFeed = request2.Execute();
                foreach (var list in gaFeed.Rows)
                {
                    var row = (List<string>)list;
                    var u = new UserLoc
                    {
                        Latitude = Double.Parse(row[0]),
                        Longitude = Double.Parse(row[1]),
                        Count = Int32.Parse(row[4])
                    };
                    if (u.Latitude != 0 && u.Longitude != 0)
                        dayUsers.Add(u);
                }
                dayUsers = condenseLocs(dayUsers);
                if (t.Hour == 2)
                {
                    deleteAll("AggregateWorld");
                }
                insert(dayUsers, "AggregateWorld");
                updateAmericaDB(dayUsers);
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                Console.Write(e.ToString());
            }
        }
        public async void updateAmericaDB(List<UserLoc> worldUsers)
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
            DateTime t = DateTime.Now;
            var request2 = service.Data.Ga.Get("ga:" + 67281419, t.ToString("yyyy-MM-dd"), t.AddDays(1).ToString("yyyy-MM-dd"), "ga:users");
            request2.Dimensions = "ga:latitude,ga:longitude,ga:hour,ga:country";
            request2.Sort = "-ga:hour";
            request2.MaxResults = 10000;
            int hour = t.Hour - 2;
            if (hour < 0) hour += 24;
            string h = hour.ToString("00");
            request2.Filters = "ga:hour==" + h + ";ga:country==United States,ga:country==Canada,ga:country==United Kingdom";
            var dayUsers = ReadFromDb("AggregateAmerica").ToList();
            try
            {
                var gaFeed = request2.Execute();
                foreach (var list in gaFeed.Rows)
                {
                    var row = (List<string>)list;
                    var u = new UserLoc
                    {
                        Latitude = Double.Parse(row[0]),
                        Longitude = Double.Parse(row[1]),
                        Count = Int32.Parse(row[4])
                    };
                    if (u.Latitude != 0 && u.Longitude != 0)
                        dayUsers.Add(u);
                }
                worldUsers.AddRange(dayUsers);
                worldUsers = condenseLocs(worldUsers);
                dayUsers = condenseLocs(dayUsers);
                if (t.Hour == 2)
                {
                    deleteAll("AggregateAmerica");
                    deleteAll("AggregateData");
                }
                insert(dayUsers, "AggregateAmerica");
                insert(worldUsers, "AggregateData");
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                return;
            }
        }
    }
}
