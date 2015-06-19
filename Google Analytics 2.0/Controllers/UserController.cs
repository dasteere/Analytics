using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Google.Apis.Analytics.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google_Analytics_2._0.Models;

namespace Google_Analytics_2._0.Controllers
{
    //api available from client
    public class UserController : ApiController
    {
        //returns all data collected throughout the day (whole world)
        [Route("api/user/day")]
        public IEnumerable<UserLoc> GetDayLocs()
        {
            return ReadFromDb("AggregateData");
        }

        //returns all of the locations where there was an increase in real time users over past 30 seconds
        [Route("api/user/change")]
        public IEnumerable<UserLoc> GetRecentChangeLocs()
        {
            return ReadFromDb("RecentChange");
        }

        //returns all of the realtime city data
        [Route("api/user/city")]
        public IEnumerable<PlaceInfo> GetCities()
        {
            return ReadFromCityDb("TopCities");
        }

        //returns all of the realtime country data
        [Route("api/user/country")]
        public IEnumerable<CityInfo> GetCurCityCountries()
        {
            return ReadFromPlaceDb("TopCountries");
        }

        //returns top 5 real time countries
        [Route("api/user/countries")]
        public IEnumerable<CityInfo> GetCountries()
        {
            return ReadFromPlaceDb("Countries");
        }

        //returns top 5 real time cities
        [Route("api/user/cities")]
        public IEnumerable<PlaceInfo> GetAllCities()
        {
            return ReadFromCityDb("Cities");
        }

        //returns aggregate locations for big countries
        [Route("api/user/america")]
        public IEnumerable<UserLoc> getAmericaLocs()
        {
            return ReadFromDb("AggregateAmerica");
        }

        [Route("api/user/world")]
        public IEnumerable<UserLoc> getWorldLocs()
        {
            return ReadFromDb("AggregateWorld");
        } 
        public IEnumerable<UserLoc> ReadFromDb(string db)
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

        public IEnumerable<CityInfo> ReadFromPlaceDb(string db)
        {
            SqlConnection conn = new SqlConnection();
            conn.ConnectionString =
                "Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False";
            conn.Open();
            string str = "SELECT * FROM " + db;
            var cmd = new SqlCommand(str, conn);
            List<CityInfo> persons = new List<CityInfo>();
            var reader = cmd.ExecuteReader();
            if (reader.HasRows)
            {
                // Read advances to the next row.
                //Console.Write("Updating row \n");
                while (reader.Read())
                {
                    persons.Add(new CityInfo
                    {
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Count = reader.GetInt32(reader.GetOrdinal("Count"))
                    });
                }
            }
            return persons;
        }
        public IEnumerable<PlaceInfo> ReadFromCityDb(string db)
        {
            SqlConnection conn = new SqlConnection();
            conn.ConnectionString =
                "Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False";
            conn.Open();
            string str = "SELECT * FROM " + db;
            var cmd = new SqlCommand(str, conn);
            List<PlaceInfo> persons = new List<PlaceInfo>();
            var reader = cmd.ExecuteReader();
            if (reader.HasRows)
            {
                // Read advances to the next row.
                //Console.Write("Updating row \n");
                while (reader.Read())
                {
                    persons.Add(new PlaceInfo
                    {
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Country = reader.GetString(reader.GetOrdinal("Country")),
                        Count = reader.GetInt32(reader.GetOrdinal("Count"))
                    });
                }
            }
            return persons;
        }
    }

    public class PlaceInfo
    {
        public string Name { get; set; }
        public string Country { get; set; }
        public int Count { get; set; }
    }
}