# https://quantpedia.com/Screener/Details/15
#import pandas as pd
from datetime import datetime

class CountryEquityIndexesMomentumAlgorithm(QCAlgorithm):

    def Initialize(self):

        self.SetStartDate(2018, 3, 1)  
        self.SetEndDate(2018, 9, 12)  
        self.SetCash(10000) 
        # create a dictionary to store momentum indicators for all symbols 
        self.data = {}
        period = 6*21
        # 
        self.symbols = ["AMZN",  #"Amazon.com"
                        "NFLX",  #"Netflix"
                        "NVDA",  #"NVIDIA"
                        "MSFT",  #"Microsoft"
                        "BA",  #"Boeing"
                        "CSCO",  #"Cisco"
                        "AAPL",  #"Apple"
                        "V",  #"Visa"
                        "HD",  #"Home Depot"
                        "UNH",  #"UnitedHealth"
                        "BAC",  #"Bank of America"
                        "JPM",  #"JPMorgan"
                        "GOOGL",  #"Alphabet A"
                        "INTC",  #"Intel"
                        "PFE",  #"Pfizer"
                        "WMT",  #"Walmart"
                        "BRKb",  #"Berkshire Hathaway B"
                        "VZ",  #"Verizon"
                        "DIS",  #"Walt Disney"
                        "WFC",  #"Wells Fargo&Co"
                        "JNJ",  #"J&J"
                        "XOM",  #"Exxon Mobil"
                        "CVX",  #"Chevron"
                        "C",  #"Citigroup"
                        "CMCSA",  #"Comcast"
                        "FB",  #"Facebook"
                        "ORCL",  #"Oracle"
                        "T",  #"AT&T"
                        "BABA",  #"Alibaba"
                        "TSLA",  #"Tesla"
                        "IVV" #Shares of SP500
] 

        # warm up the MOM indicator
        self.SetWarmUp(period)
        for symbol in self.symbols:
            self.AddEquity(symbol, Resolution.Daily)
            self.data[symbol] = self.MOM(symbol, period, Resolution.Daily)
        # shcedule the function to fire at the month start 
        self.Schedule.On(self.DateRules.EveryDay("IVV"), self.TimeRules.AfterMarketOpen("IVV"), self.Rebalance)
            
    def OnData(self, data):
        pass

    def Rebalance(self):
        if self.IsWarmingUp: return
        top = pd.Series(self.data).sort_values(ascending = False)[:9]
        for kvp in self.Portfolio:
            security_hold = kvp.Value
            # liquidate the security which is no longer in the top momentum list
            if security_hold.Invested and (security_hold.Symbol.Value not in top.index):
                self.Liquidate(security_hold.Symbol)
        
        added_symbols = []        
        for symbol in top.index:
            if not self.Portfolio[symbol].Invested:
                added_symbols.append(symbol)
        for added in added_symbols:
            self.SetHoldings(added, 1/len(added_symbols))