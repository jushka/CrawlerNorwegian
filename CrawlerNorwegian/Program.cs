using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using HtmlAgilityPack;
using System.Threading.Tasks;
using System.Data.Odbc;

namespace CrawlerNorwegian
{
    class Program
    {
        static readonly HttpClientHandler handler = new HttpClientHandler()
        {
            AllowAutoRedirect = false
        };
        static readonly HttpClient httpClient = new HttpClient(handler);

        static async Task Main()
        {
            string departureAirport = "OSL";
            string arrivalAirport = "RIX";
            int departureYear = 2019;
            int departureMonth = 10;
            int departureDayFrom = 1;
            int departureDayTo = 31;

            // gets one way, direct flights' data from norwegian.com
            await StartCrawler(departureAirport, arrivalAirport, departureYear, departureMonth, departureDayFrom, departureDayTo);
            Console.ReadLine();
        }

        private static async Task StartCrawler(string departureAirport, string arrivalAirport, int departureYear, int departureMonth, int departureDayFrom, int departureDayTo)
        {
            string departureYearMonth = departureYear.ToString() + departureMonth.ToString();
            List<Flight> flights = new List<Flight>();
            List<int> departureDays = new List<int>();
            for(int i = departureDayFrom; i <= departureDayTo; i++)
            {
                departureDays.Add(i);
            }

            foreach(int day in departureDays)
            {
                string departureDay = day.ToString();
                string url = $"https://www.norwegian.com/en/ipc/availability/avaday?D_City={departureAirport}&A_City={arrivalAirport}&TripType=1&D_Day={departureDay}&D_Month={departureYearMonth}&D_SelectedDay={departureDay}&R_Day={departureDay}&R_Month={departureMonth}&R_SelectedDay={departureDay}&IncludeTransit=false&AgreementCodeFK=-1&CurrencyCode=EUR&mode=ab";

                HttpResponseMessage response = await httpClient.GetAsync(url);
                string responseString = await response.Content.ReadAsStringAsync();

                HtmlDocument htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(responseString);

                List<HtmlNode> oddRowsInfo1 = htmlDocument.DocumentNode.Descendants("tr").Where(node => node.GetAttributeValue("class", "").Equals("oddrow rowinfo1 ")).ToList();
                List<HtmlNode> oddRowsInfo2 = htmlDocument.DocumentNode.Descendants("tr").Where(node => node.GetAttributeValue("class", "").Equals("oddrow rowinfo2")).ToList();
                List<HtmlNode> evenRowsInfo1 = htmlDocument.DocumentNode.Descendants("tr").Where(node => node.GetAttributeValue("class", "").Equals("evenrow rowinfo1 ")).ToList();
                List<HtmlNode> evenRowsInfo2 = htmlDocument.DocumentNode.Descendants("tr").Where(node => node.GetAttributeValue("class", "").Equals("evenrow rowinfo2")).ToList();
                string flightDate = $"{departureYear.ToString()}-{departureMonth.ToString()}-{day.ToString()} ";

                flights = flights.Concat(CreateFlights(oddRowsInfo1, oddRowsInfo2, flightDate)).ToList();
                flights = flights.Concat(CreateFlights(evenRowsInfo1, evenRowsInfo2, flightDate)).ToList();
            }

            SaveFlightsToDB(flights);

            Console.WriteLine("Success!");
            Console.WriteLine("Press ENTER to exit");
            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                System.Environment.Exit(0);
            }
        }

        private static List<Flight> CreateFlights(List<HtmlNode> row1, List<HtmlNode> row2, string flightDate)
        {
            List<Flight> flights = new List<Flight>();
            for(int i = 0; i < row1.Count; i++)
            {
                string price;
                if(row1[i].Descendants("td").Where(node => node.GetAttributeValue("class", "").Equals("fareselect standardlowfare")).FirstOrDefault() == null)
                {
                    price = row1[i].Descendants("td").Where(node => node.GetAttributeValue("class", "").Equals("nofare standardlowfare")).FirstOrDefault().InnerText;
                }
                else
                {
                    price = row1[i].Descendants("td").Where(node => node.GetAttributeValue("class", "").Equals("fareselect standardlowfare")).FirstOrDefault().InnerText;
                }

                Flight flight = new Flight
                {
                    DepAirport = row2[i].Descendants("td").Where(node => node.GetAttributeValue("class", "").Equals("depdest")).FirstOrDefault().InnerText,
                    ArrAirport = row2[i].Descendants("td").Where(node => node.GetAttributeValue("class", "").Equals("arrdest")).FirstOrDefault().InnerText,
                    DepTime = flightDate + row1[i].Descendants("td").Where(node => node.GetAttributeValue("class", "").Equals("depdest")).FirstOrDefault().InnerText,
                    ArrTime = flightDate + row1[i].Descendants("td").Where(node => node.GetAttributeValue("class", "").Equals("arrdest")).FirstOrDefault().InnerText,
                    Price = price
                };
                flights.Add(flight);
            }
            return flights;
        }

        private static void SaveFlightsToDB(List<Flight> flights)
        {
            string myConnection = "DRIVER={MariaDB ODBC 3.1 Driver};SERVER=localhost;DATABASE=crawler;USER=root;PASSWORD=";
            OdbcConnection con = new OdbcConnection(myConnection);
            con.Open();

            try
            {
                foreach (Flight flight in flights)
                {
                    string query = "insert into flights(DepAirport,ArrAirport,DepTime,ArrTime,Price) value(?,?,?,?,?);";
                    OdbcCommand cmd = new OdbcCommand(query, con);
                    cmd.Parameters.Add("?DepAirport", OdbcType.VarChar).Value = flight.DepAirport;
                    cmd.Parameters.Add("?ArrAirport", OdbcType.VarChar).Value = flight.ArrAirport;
                    cmd.Parameters.Add("?DepTime", OdbcType.VarChar).Value = flight.DepTime;
                    cmd.Parameters.Add("?ArrTime", OdbcType.VarChar).Value = flight.ArrTime;
                    cmd.Parameters.Add("?Price", OdbcType.VarChar).Value = flight.Price;
                    OdbcDataReader reader = cmd.ExecuteReader();
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            con.Close();
        }
    }
}