from datetime import datetime
import decimal

class DualThrustAlgorithm(QCAlgorithm):

    def Initialize(self):
        
        '''Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.'''
        
        self.SetStartDate(2010, 1, 1)
        self.SetEndDate(2018, 8, 31)
        self.SetCash(100000)
        equity = self.AddSecurity(SecurityType.Equity, "NFLX", Resolution.Hour)
        self.syl = equity.Symbol
        
        # schedule an event to fire every trading day for a security 
        # the time rule here tells it to fire when market open 
        
        self.Schedule.On(self.DateRules.EveryDay(self.syl),self.TimeRules.AfterMarketOpen(self.syl,0),Action(self.SetSignal))
        self.selltrig = None
        self.buytrig = None
        self.currentopen = None
    
    def SetSignal(self):
        history = self.History([self.syl.Value], 4, Resolution.Daily)
    
        k1 = 0.5
        k2 = 0.5
        self.high = history.loc[self.syl.Value]['high']
        self.low = history.loc[self.syl.Value]['low']
        self.close = history.loc[self.syl.Value]['close']
        
        self.currentopen = self.Portfolio[self.syl].Price
        
        HH, HC, LC, LL = max(self.high), max(self.close), min(self.close), min(self.low)
        if HH - LC >= HC - LL:
            signalrange = HH - LC
        else:
            signalrange = HC - LL
        
        self.selltrig = float(self.currentopen) - float(k1) * signalrange
        self.buytrig = float(self.currentopen) + float(k2) * signalrange    
    
    def OnData(self,data):
        
        '''OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.'''
        
        holdings = self.Portfolio[self.syl].Quantity
        
        if self.Portfolio[self.syl].Price >= self.selltrig:
            if holdings >= 0:
                self.SetHoldings(self.syl, 0.8)
            else:
                self.Liquidate(self.syl)
                self.SetHoldings(self.syl, 0.8)
                
        elif self.Portfolio[self.syl].Price < self.selltrig:
             if holdings >= 0:
                self.Liquidate(self.syl)
                self.SetHoldings(self.syl, -0.8)
             else:
                self.SetHoldings(self.syl, -0.8)
                
        self.Log("open: "+ str(self.currentopen)+" buy: "+str(self.buytrig)+" sell: "+str(self.selltrig))