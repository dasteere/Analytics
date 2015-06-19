using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Google_Analytics_2._0.Models
{
    public class UserLoc
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Count { get; set; }
        public int Hour { get; set; }
    }
}