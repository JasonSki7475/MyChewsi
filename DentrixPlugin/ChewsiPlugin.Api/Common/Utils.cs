using System;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using NLog;

namespace ChewsiPlugin.Api.Common
{
    public static class Utils
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string Address = "http://{0}:45000/DentalApi.svc";

        public static string GetPluginVersion()
        {
            return Assembly.GetCallingAssembly().GetName().Version.ToString();
        }

        public static string GetAddressFromHost(string serverHost)
        {
            if (serverHost == null)
            {
                return null;
            }
            return string.Format(Address, serverHost);
        }

        public static string GetHostFromAddress(string address)
        {
            if (address == null)
            {
                return null;
            }
            return new Uri(address).Host;
        }

        public static string GetOperatingSystemInfo()
        {
            return Environment.OSVersion.ToString();
        }

        /// <summary>
        /// Compares two DateTimes with 1s accuracy
        /// </summary>
        public static bool ArePMSModifiedDatesEqual(DateTime d1, DateTime d2)
        {
            return Math.Abs((d1 - d2).TotalSeconds) < 1;
        }

        public static void SleepWithCancellation(CancellationToken token, int sleepMs)
        {
            for (int i = 0; i < sleepMs / 100; i++)
            {
                Thread.Sleep(100);
                if (token.IsCancellationRequested)
                {
                    token.ThrowIfCancellationRequested();
                }                
            }
        }

        public static bool SafeCall<T>(Action<T> action, T arg)
        {
            try
            {
                action.Invoke(arg);
                return true;
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server side");
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communication error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            return false;
        }

        public static bool SafeCall(Action action)
        {
            try
            {
                action.Invoke();
                return true;
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server side");
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communication error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            return false;
        }

        public static bool SafeCall<T1,T2,T3>(Action<T1,T2,T3> action, T1 arg1, T2 arg2, T3 arg3)
        {
            try
            {
                action.Invoke(arg1, arg2, arg3);
                return true;
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server side");
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communication error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            return false;
        }

        public static bool TrySafeCall<T>(Func<T> action, out T result)
        {
            try
            {
                result = action.Invoke();
                return true;
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server side");
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communication error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            result = default(T);
            return false;
        }

        public static bool TrySafeCall<T, R>(Func<T, R> action, T arg, out R result)
        {
            try
            {
                result = action.Invoke(arg);
                return true;
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server side");
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communication error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            result = default(R);
            return false;
        }

        public static bool TrySafeCall<T1, T2, T3, R>(Func<T1, T2, T3, R> action, T1 arg1, T2 arg2, T3 arg3, out R result)
        {
            try
            {
                result = action.Invoke(arg1, arg2, arg3);
                return true;
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server side");
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communication error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            result = default(R);
            return false;
        }
    }
}
