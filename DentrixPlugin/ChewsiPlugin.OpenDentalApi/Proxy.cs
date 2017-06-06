using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using ChewsiPlugin.OpenDentalApi.DTO;
using NLog;

namespace ChewsiPlugin.OpenDentalApi
{
    internal class Proxy : MarshalByRefObject
    {
        private Logger _logger;
        private Type _type;
        private Object _object;
        private string _openDentBusinessDllPath;

        static Proxy()
        {
            /*
             Increase default lease time for remoting.
             More details here: "Managing the Lifetime of Remote .NET Objects with Leasing and Sponsorship", MSDN Magazine Dec 2003
             Also see RenewLease() method
             */

            LifetimeServices.LeaseTime = TimeSpan.FromMinutes(10);
            LifetimeServices.RenewOnCallTime = TimeSpan.FromMinutes(15);
        }

        public void SetLogger(Logger logger)
        {
            _logger = logger;
        }

        public void InstantiateObject(string assemblyPath, string typeName, object[] args)
        {
            // LoadFrom loads dependent DLLs (assuming they are in the app domain's base directory
            var assembly = Assembly.LoadFrom(assemblyPath);

            _type = assembly.GetType(typeName);
            _object = Activator.CreateInstance(_type, args);
        }

        public object InvokeMethod(string name, object[] args)
        {
            RenewLease();

            var methodinfo = _type.GetMethod(name);
            return methodinfo.Invoke(_object, args);
        }
        
        public void SetField(string name, object value)
        {
            RenewLease();

            var prop = _type.GetField(name);
            prop.SetValue(_object, value);
        }

        private static TTo MapList<TTo, TToItem>(object from) where TTo : IList, new()
                                                    where TToItem : new()
        {
            var result = new TTo();
            foreach (var f in (IEnumerable)from)
            {
                result.Add(Map<TToItem>(f));
            }
            return result;
        }

        private static TTo Map<TTo>(object from) where TTo : new()
        {
            var result = new TTo();
            var srcFields = from.GetType().GetFields();
            foreach (var propertyInfo in typeof(TTo).GetProperties())
            {
                var srcField = srcFields.First(m => m.Name == propertyInfo.Name);

                // Stringify enums, because we don't have references to the assembly with the type from.GetType()
                object val = srcField.GetValue(from);
                if (val.GetType().IsEnum)
                {
                    val = val.ToString();
                }

                propertyInfo.SetValue(result, val, null);
            }
            return result;
        }

        private TTo InvokeStaticMethod<TTo>(string assemblyPath, string typeName, string methodName, object[] args) where TTo : new()
        {
            var result = InvokeStaticMethodWithoutMapping(assemblyPath, typeName, methodName, args);
            return Map<TTo>(result);
        }

        public void RenewLease()
        {
            ILease lease = (ILease) RemotingServices.GetLifetimeService(this);
            Debug.Assert(lease.CurrentState == LeaseState.Active);
            lease.Renew(TimeSpan.FromMinutes(30));
        }

        private object InvokeStaticMethodWithoutMapping(string assemblyPath, string typeName, string methodName, object[] args)
        {
            try
            {
                RenewLease();

                // LoadFrom loads dependent DLLs from the app domain's base directory
                var assembly = Assembly.LoadFrom(assemblyPath);
                var type = assembly.GetType(typeName);
                var methodinfo = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public, null,
                    args?.Select(m => m.GetType()).ToArray() ?? new Type[0], null);
                return methodinfo.Invoke(null, args);
            }
            catch (RemotingException e)
            {
                _logger?.Error(e, "Remoting exception has been handled");
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                throw;
            }
            return null;
        }

        private TTo InvokeStaticMethodForList<TTo, TToItem>(string assemblyPath, string typeName, string methodName, object[] args)
            where TTo : IList, new()
            where TToItem : new()
        {
            var result = InvokeStaticMethodWithoutMapping(assemblyPath, typeName, methodName, args);
            return MapList<TTo, TToItem>(result);
        }

        public void Initialize(string openDentBusinessDllPath)
        {
            _openDentBusinessDllPath = openDentBusinessDllPath;
        }
        
        public PatientInfo GetPatient(long patientId)
        {
            return InvokeStaticMethod<PatientInfo>(_openDentBusinessDllPath, "OpenDentBusiness.Patients", "GetPat", new object[] { patientId });
        }

        public ProviderInfo GetProvider(long providerId)
        {
            return InvokeStaticMethod<ProviderInfo>(_openDentBusinessDllPath, "OpenDentBusiness.Providers", "GetProv", new object[] { providerId });
        }

        public List<ProcedureInfo> GetProcedures(long patientId)
        {
            return InvokeStaticMethodForList<List<ProcedureInfo>, ProcedureInfo>(_openDentBusinessDllPath, "OpenDentBusiness.Procedures", "GetCompleteForPats", new object[] { new List<long> { patientId } });
        }
        public List<ProcedureInfo> GetProceduresByAppointment(long appointmentId)
        {
            return InvokeStaticMethodForList<List<ProcedureInfo>, ProcedureInfo>(_openDentBusinessDllPath, "OpenDentBusiness.Procedures", "GetProcsMultApts", new object[] { new List<long> { appointmentId }, false });
        }

        public List<ProcedureCodeInfo> GetAllCodes()
        {
            return InvokeStaticMethodForList<List<ProcedureCodeInfo>, ProcedureCodeInfo>(_openDentBusinessDllPath, "OpenDentBusiness.ProcedureCodes", "GetAllCodes", null);
        }
        
        public List<CarrierInfo> CarriersGetSimilarNames(string carrierName)
        {
            return InvokeStaticMethodForList<List<CarrierInfo>, CarrierInfo>(_openDentBusinessDllPath, "OpenDentBusiness.Carriers", "GetSimilarNames", new object[] { carrierName });
        }

        public List<long> InsPlansGetPlanNumsByCarrierNum(long carrierNum)
        {
            return InvokeStaticMethodForList<List<long>, long>(_openDentBusinessDllPath, "OpenDentBusiness.InsPlans", "GetPlanNumsByCarrierNum", new object[] { carrierNum });
        }
        
        public List<SubscriberInfo> GetSubscribers(long patientId)
        {
            var patPlans = InvokeStaticMethodForList<List<PatPlanInfo>, PatPlanInfo>(_openDentBusinessDllPath, "OpenDentBusiness.PatPlans", "Refresh", new object[] { patientId });

            var fam = InvokeStaticMethodWithoutMapping(_openDentBusinessDllPath, "OpenDentBusiness.Patients", "GetFamily", new object[] { patientId });
            var insSubs = InvokeStaticMethodForList<List<InsSubInfo>, InsSubInfo>(_openDentBusinessDllPath, "OpenDentBusiness.InsSubs", "RefreshForFam", new[] { fam });

            var subscribers = from plan in patPlans
                join insSubInfo in insSubs on plan.InsSubNum equals insSubInfo.InsSubNum
                select new SubscriberInfo
                {
                    ChewsiId = insSubInfo.SubscriberID,
                    PatientInfo = GetPatient(insSubInfo.Subscriber)
                };
            
            return subscribers.ToList();
        }

        public List<AppointmentInfo> GetAppointmentsStartingWithinPeriod(DateTime from, DateTime to)
        {
            return InvokeStaticMethodForList<List<AppointmentInfo>, AppointmentInfo>(_openDentBusinessDllPath, "OpenDentBusiness.Appointments", "GetAppointmentsStartingWithinPeriod", new object[] { from, to });
        }
    }
}
