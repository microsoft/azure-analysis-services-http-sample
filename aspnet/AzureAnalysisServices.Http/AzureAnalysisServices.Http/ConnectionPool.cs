using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Samples.AzureAnalysisServices.Http
{
    internal class ConnectionPoolEntry
    {
            
        public ConnectionPoolEntry(AdomdConnection con, string connectionString)
        {
            this.Connection = con;
            this.ConnectionString = connectionString;

            //the combindation of the strong reference to the connection
            //and this delegate ties the reachability of the ConnectionPoolEntry and the AdomdConnection together
            //so they are guaranteed to become unreachable at the same time
            //This would enable the ConnectionPool to keep a WeakReference to the ConnectionPoolEntry without
            //keeping the AdomdConnection alive, but also not worry about the ConnectionPoolEntry being GCd
            //while the AdomdConnection is still alive.
            con.Disposed += (s, a) =>
            {
                this.IsDisposed = true;
                con = null;
            };

            
        }
        
        public bool IsDisposed { get; private set; } = false;

        public string ConnectionString { get; private set; }

        public AdomdConnection Connection { get; private set; }

        public DateTime ValidTo { get; set; }

        public void RecordCheckIn()
        {
            IsCheckedOut = false;
            TotalCheckoutTime += DateTime.Now.Subtract(LastCheckedOut);
            LastCheckedIn = DateTime.Now;

        }

        public void RecordCheckOut()
        {
            IsCheckedOut = true;
            LastCheckedOut = DateTime.Now;
        }
        public bool IsCheckedOut { get; private set; }
        public int TimesCheckedOut { get; private set; }
        public TimeSpan TotalCheckoutTime { get; private set; }
        public DateTime LastCheckedOut { get; private set; } = DateTime.MinValue;
        public DateTime LastCheckedIn { get; private set; } = DateTime.MinValue;
    }

    //This is a simple Connection Pool, made simple because the client 
    //is responsible for checking out and back in the ConnectionPoolEntry, not just the AdomdConneciton.
    //The ConnectionPoolEntry has a reference to the AdomdConnection and vice versa so they have the same lifetime.
    //If a client checks out a ConnectionPoolEntry and doesn't return it, the ConnectionPool retains no reference to it.
    internal class ConnectionPool
    {
        public static ConnectionPool Instance = new ConnectionPool();

        ConcurrentDictionary<string, ConcurrentStack<ConnectionPoolEntry>> avalableConnections = new ConcurrentDictionary<string, ConcurrentStack<ConnectionPoolEntry>>();

        public void ReturnConnection(ConnectionPoolEntry entry)
        {
            var key = entry.ConnectionString;
            entry.RecordCheckIn();
            avalableConnections[key].Push(entry);
        }
        public ConnectionPoolEntry GetConnection(string connectionString, AuthData authData)
        {
            var key = connectionString;
            
            ConnectionPoolEntry rv = null;
            avalableConnections.AddOrUpdate(key, k => new ConcurrentStack<ConnectionPoolEntry>(), (k, c) =>
            {
                while (c.TryPop( out var entry ))
                {
                    //if we discover that the entry has expired, dispose of it
                    //this typically happens when the entry uses BEARER auth and its token
                    //has expired (or is about to).
                    if (entry.ValidTo > DateTime.Now.Subtract(TimeSpan.FromMinutes(1)))
                    {
                        entry.Connection.Dispose();
                        continue;
                    }

                    rv = entry;
                    break;
                }
     
                return c;
            });

            if (rv == null)
            {

                var con = new AdomdConnection(connectionString);
                rv = new ConnectionPoolEntry(con, connectionString);

                var validTo = DateTime.Now.AddHours(1); //default
                if (authData.Scheme ==  AuthScheme.BEARER)
                {
                    var token = TokenHelper.ReadToken(authData.PasswordOrToken);
                    validTo = token.ValidTo.ToLocalTime();
                }
                rv.ValidTo = validTo;

                con.Open();
  

            }

            rv.RecordCheckOut();
            return rv;
        }
    }
}
