using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Samples.XMLA.HTTP.Proxy
{
    public class ConnectionPoolEntry
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

        [System.Text.Json.Serialization.JsonIgnore]
        public string ConnectionString { get; private set; }

        [System.Text.Json.Serialization.JsonIgnore]
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
            TimesCheckedOut += 1;
        }
        public bool IsCheckedOut { get; private set; }
        public int TimesCheckedOut { get; private set; } = 0;

        [System.Text.Json.Serialization.JsonIgnore]
        public TimeSpan TotalCheckoutTime { get; private set; }
        public DateTime LastCheckedOut { get; private set; } = DateTime.MinValue;
        public DateTime LastCheckedIn { get; private set; } = DateTime.MinValue;
        public DateTime CreatedAt { get; private set; } = DateTime.Now;

        public override string ToString()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }
    }

    //This is a simple Connection Pool, made simple because the client 
    //is responsible for checking out and back in the ConnectionPoolEntry, not just the AdomdConneciton.
    //The ConnectionPoolEntry has a reference to the AdomdConnection and vice versa so they have the same lifetime.
    //If a client checks out a ConnectionPoolEntry and doesn't return it, the ConnectionPool retains no reference to it.
    public class ConnectionPool
    {

        ConcurrentDictionary<string, ConcurrentStack<ConnectionPoolEntry>> avalableConnections = new ConcurrentDictionary<string, ConcurrentStack<ConnectionPoolEntry>>();
        private TokenHelper tokenHelper;

        public ConnectionPool(TokenHelper tokenHelper)
        {
            this.tokenHelper = tokenHelper;
        }


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
                    if ( DateTime.Now > entry.ValidTo.Subtract(TimeSpan.FromMinutes(1)) )
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

                var validTo = DateTime.Now.AddMinutes(5); //default
                if (authData.Scheme ==  AuthScheme.BEARER)
                {
                    var token = tokenHelper.ReadToken(authData.PasswordOrToken);
                    if (validTo > token.ValidTo.ToLocalTime())
                    {
                        validTo = token.ValidTo.ToLocalTime();
                    }
                }
                rv.ValidTo = validTo;

                con.Open();

            }

            rv.RecordCheckOut();
            return rv;
        }
    }
}
