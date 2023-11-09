using MongoDB.Bson;
using MongoDB.Driver;
using RestSharp;
using Skymey_crypto_binance_current_price.Data;
using Skymey_main_lib.Models.Prices.Okex;
using Skymey_main_lib.Models.Prices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Json;
using Skymey_main_lib.Models.Prices.Binance;

namespace Skymey_crypto_binance_current_price.Actions.GetPrices
{
    public class GetPrices
    {
        private RestClient _client;
        private RestRequest _request;
        private MongoClient _mongoClient;
        private ApplicationContext _db;
        public GetPrices()
        {
            _client = new RestClient("https://api.binance.com/api/v1/ticker/price");
            _request = new RestRequest("https://api.binance.com/api/v1/ticker/price", Method.Get);
            _mongoClient = new MongoClient("mongodb://127.0.0.1:27017");
            _db = ApplicationContext.Create(_mongoClient.GetDatabase("skymey"));
        }
        public double TruncateToSignificantDigits(double d, int digits)
        {
            if (d == 0)
                return 0;

            double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1 - digits);
            return scale * Math.Truncate(d / scale);
        }
        public void GetCurrentPricesFromBinance()
        {
            _request.AddHeader("Content-Type", "application/json");
            var r = _client.Execute(_request).Content;
            List<BinanceCurrentPrice> ticker = new JavaScriptSerializer().Deserialize<List<BinanceCurrentPrice>>(r);
            foreach (var tickers in ticker)
            {
                Console.WriteLine(tickers.symbol);
                var ticker_find = (from i in _db.BinanceCurrentPriceView where i.symbol == tickers.symbol select i).FirstOrDefault();
                string ticker_search = tickers.symbol;
                var ticker_findc = (from i in _db.CurrentPrices where i.Ticker == ticker_search select i).FirstOrDefault();
                if (ticker_find == null)
                {
                    BinanceCurrentPrice ocp = new BinanceCurrentPrice();
                    ocp._id = ObjectId.GenerateNewId();
                    ocp.symbol = tickers.symbol;
                    ocp.price = tickers.price;
                    ocp.Update = DateTime.UtcNow;
                    _db.BinanceCurrentPriceView.Add(ocp);
                }
                else
                {
                    ticker_find.price = tickers.price;
                    ticker_find.Update = DateTime.UtcNow;
                    _db.BinanceCurrentPriceView.Update(ticker_find);
                }
                if (ticker_findc == null)
                {
                    CurrentPrices ocpc = new CurrentPrices();
                    ocpc._id = ObjectId.GenerateNewId();
                    ocpc.Ticker = ticker_search;
                    ocpc.Price = Convert.ToDouble(tickers.price.Replace(".",","));
                    ocpc.Update = DateTime.UtcNow;
                    _db.CurrentPrices.Add(ocpc);
                }
                else
                {
                    ticker_findc.Price = (ticker_findc.Price + Convert.ToDouble(tickers.price.Replace(".", ","))) / 2;
                    ticker_findc.Update = DateTime.UtcNow;
                    _db.CurrentPrices.Update(ticker_findc);
                }

            }
            _db.SaveChanges();
        }
        
    }
}
