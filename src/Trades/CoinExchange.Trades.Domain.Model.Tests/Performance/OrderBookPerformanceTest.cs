/***************************************************************************** 
* Copyright 2016 Aurora Solutions 
* 
*    http://www.aurorasolutions.io 
* 
* Aurora Solutions is an innovative services and product company at 
* the forefront of the software industry, with processes and practices 
* involving Domain Driven Design(DDD), Agile methodologies to build 
* scalable, secure, reliable and high performance products.
* 
* Coin Exchange is a high performance exchange system specialized for
* Crypto currency trading. It has different general purpose uses such as
* independent deposit and withdrawal channels for Bitcoin and Litecoin,
* but can also act as a standalone exchange that can be used with
* different asset classes.
* Coin Exchange uses state of the art technologies such as ASP.NET REST API,
* AngularJS and NUnit. It also uses design patterns for complex event
* processing and handling of thousands of transactions per second, such as
* Domain Driven Designing, Disruptor Pattern and CQRS With Event Sourcing.
* 
* Licensed under the Apache License, Version 2.0 (the "License"); 
* you may not use this file except in compliance with the License. 
* You may obtain a copy of the License at 
* 
*    http://www.apache.org/licenses/LICENSE-2.0 
* 
* Unless required by applicable law or agreed to in writing, software 
* distributed under the License is distributed on an "AS IS" BASIS, 
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
* See the License for the specific language governing permissions and 
* limitations under the License. 
*****************************************************************************/


﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoinExchange.Common.Domain.Model;
using CoinExchange.Trades.Domain.Model.CurrencyPairAggregate;
using CoinExchange.Trades.Domain.Model.OrderAggregate;
using CoinExchange.Trades.Domain.Model.OrderMatchingEngine;
using CoinExchange.Trades.Domain.Model.Services;
using CoinExchange.Trades.Domain.Model.TradeAggregate;
using CoinExchange.Trades.Infrastructure.Persistence.RavenDb;
using Disruptor;
using NUnit.Framework;

namespace CoinExchange.Trades.Domain.Model.Tests.Performance
{
    [TestFixture]
    class OrderBookPerformanceTest
    {
        private Exchange _exchange = null;
        private int _orderCount = 5000;

        [SetUp]
        public void Setup()
        {
            // NOTE: Passing in NULL as RavenDB event store is no longer operational
            //IEventStore eventStore = new RavenNEventStore(Constants.OUTPUT_EVENT_STORE);
            Journaler journaler = new Journaler(null);
            
            // Initialize the output Disruptor and assign the journaler as the event handler
            OutputDisruptor.InitializeDisruptor(new IEventHandler<byte[]>[] {journaler});
        }

        [TearDown]
        public void TearDown()
        {
            OutputDisruptor.ShutDown();
        }

        [Test]
        public void PerformanceTest()
        {
            // Initialize the output Disruptor and assign the journaler as the event handler

            /*// NOTE: Passing in NULL as RavenDB event store is no longer operational
            //IEventStore eventStore = new RavenNEventStore(Constants.OUTPUT_EVENT_STORE);
            Journaler journaler = new Journaler(null); 

            OutputDisruptor.InitializeDisruptor(new IEventHandler<byte[]>[] { journaler });*/
            IList<CurrencyPair> currencyPairs = new List<CurrencyPair>();
            currencyPairs.Add(new CurrencyPair("BTCLTC", "LTC", "BTC"));
            currencyPairs.Add(new CurrencyPair("BTCUSD", "USD", "BTC"));
            currencyPairs.Add(new CurrencyPair("BTCDOGE", "DOGE", "BTC"));

            _exchange = new Exchange(currencyPairs);
            List<OrderId> orderIds = new List<OrderId>();
            // Create Orders
            Order[] orders = new Order[_orderCount];
            Random random = new Random();

            var startOrderObjectCreation = DateTime.Now;
            for (int i = 0; i < orders.Length; i++)
            {
                bool isBuy = ((i % 2) == 0);
                decimal delta = isBuy ? 1880 : 1884;
                
                Price price = new Price(random.Next(1, 10) + delta);
                //Volume volume = new Volume(random.Next() % 10 + 1 * 100);
                Volume volume = new Volume(1);

                OrderId orderId = new OrderId(random.Next(1, 100).ToString(CultureInfo.InvariantCulture));
                orderIds.Add(orderId);
                orders[i] = new Order(orderId, "BTCLTC", price, isBuy ? OrderSide.Buy :
                OrderSide.Sell, OrderType.Limit,  volume, new TraderId(random.Next(1,100).ToString()));
            }

            var endOrderObjectCreation = DateTime.Now;
            Console.WriteLine("Order Objects created : {0} | Time elapsed: {1} seconds", _orderCount, (endOrderObjectCreation - startOrderObjectCreation).TotalSeconds);

            //JustAddOrdersToList(orders);
            AddOrdersAndCancel(_exchange.ExchangeEssentials.First().LimitOrderBook, orders, orderIds);
        }

        /**
         * The test case populates SELL side LIMIT Orders and then send equal number of BUY side MARKET Orders
         */
        [Test]
        public void PerformanceTestForOrderFill()
        {
            IList<CurrencyPair> currencyPairs = new List<CurrencyPair>();
            currencyPairs.Add(new CurrencyPair("BTCLTC", "LTC", "BTC"));

            _exchange = new Exchange(currencyPairs);
            List<OrderId> orderIds = new List<OrderId>();
            // Create Orders
            Order[] sellOrders = new Order[_orderCount];
            Random random = new Random();

            var startOrderObjectCreation = DateTime.Now;
            for (int i = 0; i < sellOrders.Length; i++)
            {
                decimal delta = 1884;
                Price price = new Price(1 + delta);

                Volume volume = new Volume(1);
                OrderId orderId = new OrderId((i + 1).ToString());
                orderIds.Add(orderId);
                sellOrders[i] = new Order(orderId, "BTCLTC", price, OrderSide.Sell, OrderType.Limit, volume, new TraderId(random.Next(1, 100).ToString()));
            }

            Order[] buyOrders = new Order[_orderCount];

            for (int i = 0; i < buyOrders.Length; i++)
            {
                Price price = new Price(0);
                Volume volume = new Volume(1);
                OrderId orderId = new OrderId((i + 1 + _orderCount).ToString());
                orderIds.Add(orderId);
                buyOrders[i] = new Order(orderId, "BTCLTC", price, OrderSide.Buy, OrderType.Market, volume, new TraderId(random.Next(1, 100).ToString()));
            }

            var endOrderObjectCreation = DateTime.Now;
            Console.WriteLine("Order Objects created SELL: {0} BUY: {0}| Time elapsed: {1} seconds", _orderCount, (startOrderObjectCreation - endOrderObjectCreation).TotalSeconds);

            // Add SELL side orders to Order Book
            AddOrders(_exchange.ExchangeEssentials.First().LimitOrderBook, sellOrders);

            // Add BUY side orders for Trade Execution
            AddOrders(_exchange.ExchangeEssentials.First().LimitOrderBook, buyOrders);
        }

        /// <summary>
        /// Addes provided orders to the Limit Order Book
        /// </summary>
        /// <param name="orderBook"></param>
        /// <param name="orders"></param>
        /// <returns></returns>
        private int AddOrders(LimitOrderBook orderBook, Order[] orders)
        {
            int count = 0;
            var start = DateTime.Now;
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i].Price != null && orders[i].Volume != null)
                {
                    orderBook.AddOrder(orders[i]);
                    count++;
                }
                else
                {
                    throw new Exception();
                }
            }

            var end = DateTime.Now;
            Console.WriteLine("Orders Places: {0} Time elapsed: {1} seconds", count, (end - start).TotalSeconds);
            Console.WriteLine("Bids: " + orderBook.Bids.Count() + ", Ask: " + orderBook.Asks.Count()  + ", Trades: "
                              + (_exchange.ExchangeEssentials.First().TradeListener.Trades != null 
                                    ? _exchange.ExchangeEssentials.First().TradeListener.Trades.Count() : 0));

            return count;
        }

        /// <summary>
        /// Adds and cancels orders
        /// </summary>
        /// <param name="orderBook"></param>
        /// <param name="orders"></param>
        /// <param name="orderIds"></param>
        /// <returns></returns>
        private void AddOrdersAndCancel(LimitOrderBook orderBook, Order[] orders, List<OrderId> orderIds)
        {
            Console.WriteLine(orders.Length + " orders received for sending over to exchange.");
            var overallStart = DateTime.Now;
            Console.WriteLine("Start time: " + DateTime.Now);

            int count = 0;
            var startAdd = DateTime.Now;
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i].Price != null && orders[i].Volume != null)
                {
                    orderBook.AddOrder(orders[i]);
                    count++;
                }
                else
                {
                    throw new Exception();
                }
            }

            var endAdd = DateTime.Now;
            Console.WriteLine("Orders added. : {0} | Time elapsed: {1} seconds", count, (endAdd - startAdd).TotalSeconds);
            Console.WriteLine("Bids: " + orderBook.Bids.Count() + ", Ask: " + orderBook.Asks.Count() + ", Trades: " + _exchange.ExchangeEssentials.First().TradeListener.Trades.Count());

            var startCancel = DateTime.Now;
            count = 0;
            foreach (OrderId orderId in orderIds)
            {
                if (orderId != null)
                {
                    if (orderBook.CancelOrder(orderId))
                    {
                        count++;
                    }
                }
                else
                {
                    throw new Exception();
                }
            }

            var endCancel = DateTime.Now;
            Console.WriteLine("Orders cancelled. : {0} | Time elapsed: {1} seconds", count, (endCancel - startCancel).TotalSeconds);
            Console.WriteLine("Bids: " + orderBook.Bids.Count() + ", Ask: " + orderBook.Asks.Count());
            
            var overAllEnd = DateTime.Now;
            Console.WriteLine("Overall Operation Time elapsed: {0} seconds", (overAllEnd - overallStart).TotalSeconds);
            Console.Write("End time: " + DateTime.Now);
        }

        private void JustAddOrdersToList(Order[] orders)
        {
            int count = 0;
            List<Order> orderList = new List<Order>();
            var startAdd = DateTime.Now;
            for (int i = 0; i < orders.Length; i++)
            {
                orderList.Add(orders[i]);
                ++count;
            }

            var endAdd = DateTime.Now;
            Console.WriteLine(count + " JUST orders added. : {0} | Time elapsed: {1} seconds", count, (endAdd - startAdd).TotalSeconds);
        }
    }
}
