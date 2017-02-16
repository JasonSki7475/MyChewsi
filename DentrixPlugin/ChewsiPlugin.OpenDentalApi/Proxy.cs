using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChewsiPlugin.OpenDentalApi.DTO;

namespace ChewsiPlugin.OpenDentalApi
{
    internal class Proxy : MarshalByRefObject
    {
        private Type _type;
        private Object _object;
        private string _openDentBusinessDllPath;

        public void InstantiateObject(string assemblyPath, string typeName, object[] args)
        {
            //LoadFrom loads dependent DLLs (assuming they are in the app domain's base directory
            var assembly = Assembly.LoadFrom(assemblyPath);

            _type = assembly.GetType(typeName);
            _object = Activator.CreateInstance(_type, args);
        }

        public object InvokeMethod(string name, object[] args)
        {
            var methodinfo = _type.GetMethod(name);
            return methodinfo.Invoke(_object, args);
        }
        
        public void SetField(string name, object value)
        {
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

        public static TTo InvokeStaticMethod<TTo>(string assemblyPath, string typeName, string methodName, object[] args) where TTo : new()
        {
            var result = InvokeStaticMethodWithoutMapping(assemblyPath, typeName, methodName, args);
            return Map<TTo>(result);
        }

        private static object InvokeStaticMethodWithoutMapping(string assemblyPath, string typeName, string methodName, object[] args)
        {
            // LoadFrom loads dependent DLLs from the app domain's base directory
            var assembly = Assembly.LoadFrom(assemblyPath);
            var type = assembly.GetType(typeName);
            var methodinfo = type.GetMethod(methodName);
            return methodinfo.Invoke(null, args);
        }

        public static TTo InvokeStaticMethodForList<TTo, TToItem>(string assemblyPath, string typeName, string methodName, object[] args)
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

        public List<ProcedureCodeInfo> GetAllCodes()
        {
            return InvokeStaticMethodForList<List<ProcedureCodeInfo>, ProcedureCodeInfo>(_openDentBusinessDllPath, "OpenDentBusiness.ProcedureCodes", "GetAllCodes", null);
        }

        public InsPlanInfo GetInsPlanByCarrier(string carrierName)
        {
            return InvokeStaticMethod<InsPlanInfo>(_openDentBusinessDllPath, "OpenDentBusiness.InsPlans", "GetByCarrierName", new object[] { carrierName });
        }

        public List<AppointmentInfo> GetAppointmentsStartingWithinPeriod(DateTime from, DateTime to)
        {
            return InvokeStaticMethodForList<List<AppointmentInfo>, AppointmentInfo>(_openDentBusinessDllPath, "OpenDentBusiness.Appointments", "GetAppointmentsStartingWithinPeriod", new object[] { from, to });
        }
    }
}
