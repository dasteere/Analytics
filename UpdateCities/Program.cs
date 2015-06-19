using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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

namespace UpdateCities
{
    public class CityInfo
    {
        public string Name { get; set; }
        public string Country { get; set; }
        public int Count { get; set; }
    }

    public class CountryInfo
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var timePlace = new System.Timers.Timer(12500);
            var u = new Updater();
            timePlace.Elapsed += new ElapsedEventHandler(u.updatePlaces);
            timePlace.Interval = 12500;
            timePlace.Start();
            while (true)
            {
                Console.ReadKey(true);
            }
        }
        
    }

    public class Updater
    {

        public void updatePlaces(object u, ElapsedEventArgs eventId)
        {
            getRtPlacesData();
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
                Console.Write("ERROR in PlaceService delete: " + e);
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
        public void insertPlaces(List<CityInfo> places, List<CountryInfo> countries, string db, bool city)
        {
            try
            {
                SqlConnection conn = new SqlConnection("Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False");
                DataSet ds = city ? ToDataSet(places) : ToDataSet(countries);
                var sourceData = ds.Tables[0];
                conn.Open();
                using (SqlBulkCopy bulkCopy =
                            new SqlBulkCopy(conn.ConnectionString))
                {
                    // column mappings
                    bulkCopy.ColumnMappings.Add("Name", "Name");
                    if (city) bulkCopy.ColumnMappings.Add("Country", "Country");
                    bulkCopy.ColumnMappings.Add("Count", "Count");
                    bulkCopy.DestinationTableName = db;
                    bulkCopy.WriteToServer(sourceData);
                }
            }
            catch (Exception e)
            {
                Console.Write("ERROR in PlaceService insert: " + e);
            }
        }
        public async void getRtPlacesData()
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
                var t = DateTime.Now;
                var request = service.Data.Ga.Get("ga:" + 67281419, t.ToString("yyyy-MM-dd"), t.AddDays(1).ToString("yyyy-MM-dd"), "ga:users");
                request.Dimensions = "ga:country,ga:city";
                request.Sort = "-ga:activeUsers";
                request.MaxResults = 10000;
                var feed = request.Execute();
                List<CityInfo> cities = new List<CityInfo>();
                List<CountryInfo> countries = new List<CountryInfo>();
                foreach (var list in feed.Rows)
                {
                    var row = (List<string>)list;
                    var country = new CountryInfo()
                    {
                        Name = row[0],
                        Count = Int32.Parse(row[2])
                    };
                    var city = new CityInfo()
                    {
                        Name = row[1],
                        Country = row[0],
                        Count = Int32.Parse(row[2])
                    };
                    if (!country.Name.ToLower().Equals("zz") && !country.Name.Contains(("(Not")))
                        countries.Add(country);
                    if (!city.Name.Contains(("(Not")) && !city.Name.ToLower().Equals("zz"))
                        cities.Add(city);
                }
                countries = condensePlaces(countries);
                deleteAll("Countries");
                deleteAll("Cities");
                insertPlaces(null, countries, "Countries", false);
                insertPlaces(cities, null, "Cities", true);
                countries = countries.OrderBy(u => u.Count).ToList();
                cities = cities.OrderBy(u => u.Count).ToList();
                insertPlaces(null, countries.Take(5).ToList(), "Country", false);
                insertPlaces(cities.Take(5).ToList(), null, "City", true);
            }
            catch (Exception e)
            {
                Console.Write("ERROR in AggregateService insertPlaces: " + e);
            }
        }

        public List<CountryInfo> condensePlaces(List<CountryInfo> places)
        {
            var store = new Dictionary<string, CountryInfo>();
            foreach (var u in places)
            {
                if (store.ContainsKey(u.Name))
                {
                    store[u.Name] = new CountryInfo
                    {
                        Name = u.Name,
                        Count = store[u.Name].Count + u.Count
                    };
                }
                else
                {
                    store.Add(u.Name, u);
                }
            }
            return store.Values.ToList();
        }
    }
}
