using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChewsiPlugin.OpenDentalApi.DTO;
using OpenDentBusiness;

namespace ChewsiPlugin.OpenDentalApi
{
    internal class Proxy : MarshalByRefObject
    {
        private Type _type;
        private Object _object;
        private const string Path = @"C:\Program Files (x86)\Open Dental\OpenDentBusiness.dll";

        public void InstantiateObject(string assemblyPath, string typeName, object[] args)
        {
            //LoadFrom loads dependent DLLs (assuming they are in the app domain's base directory
            var assembly = Assembly.LoadFrom(assemblyPath);

            _type = assembly.GetType(typeName);
            _object = Activator.CreateInstance(_type, args); ;
        }

        public object InvokeMethod(string name, object[] args)
        {
            var methodinfo = _type.GetMethod(name);
            return methodinfo.Invoke(_object, args);
        }

        public void SetProperty(string name, object value)
        {
            PropertyInfo prop = _type.GetProperty(name);
            prop.SetValue(_object, value, null);
        }

        public void SetField(string name, object value)
        {
            var prop = _type.GetField(name);
            prop.SetValue(_object, value);
        }

        private TTo MapList<TFrom, TTo, TFromItem, TToItem>(TFrom from) where TTo : IList, new()
                                                    where TFrom : IList
                                                    where TFromItem : new()
                                                    where TToItem : new()
        {
            var result = new TTo();
            foreach (var f in from)
            {
                result.Add(Map<TFromItem, TToItem>((TFromItem)f));
            }
            return result;
        }

        private TTo Map<TFrom, TTo>(TFrom from) where TTo : new()
        {
            var result = new TTo();
            var srcFields = typeof(TFrom).GetFields();
            foreach (var propertyInfo in typeof(TTo).GetProperties())
            {
                var srcField = srcFields.First(m => m.Name == propertyInfo.Name);
                propertyInfo.SetValue(result, srcField.GetValue(from), null);
            }
            return result;
        }

        private TTo InvokeStaticMethod<TFrom, TTo>(string assemblyPath, string typeName, string methodName, object[] args) where TTo : new()
        {
            var result = InvokeStaticMethodWithoutMapping(assemblyPath, typeName, methodName, args);
            return Map<TFrom, TTo>((TFrom)result);
        }

        private object InvokeStaticMethodWithoutMapping(string assemblyPath, string typeName, string methodName, object[] args)
        {
            // LoadFrom loads dependent DLLs from the app domain's base directory
            var assembly = Assembly.LoadFrom(assemblyPath);
            var type = assembly.GetType(typeName);
            var methodinfo = type.GetMethod(methodName);
            return methodinfo.Invoke(null, args);
        }

        private TTo InvokeStaticMethodForList<TFrom, TTo, TFromItem, TToItem>(string assemblyPath, string typeName, string methodName, object[] args)
            where TTo : IList, new()
            where TFrom : IList
            where TFromItem : new()
            where TToItem : new()
        {
            var result = InvokeStaticMethodWithoutMapping(assemblyPath, typeName, methodName, args);
            return MapList<TFrom, TTo, TFromItem, TToItem>((TFrom)result);
        }

        public PatientInfo GetPatient(long patientId)
        {
            return InvokeStaticMethod<Patient, PatientInfo>(Path, "OpenDentBusiness.Patients", "GetPat", new object[] { patientId });
        }

        public ProviderInfo GetProvider(long providerId)
        {
            return InvokeStaticMethod<Provider, ProviderInfo>(Path, "OpenDentBusiness.Providers", "GetProv", new object[] { providerId });
        }

        public List<ProcedureInfo> GetProcedures(long patientId)
        {
            return InvokeStaticMethodForList<List<Procedure>, List<ProcedureInfo>, Procedure, ProcedureInfo>(Path, "OpenDentBusiness.Procedures", "GetCompleteForPats", new object[] { new List<long> { patientId } });
        }

        public List<ProcedureCodeInfo> GetAllCodes()
        {
            return InvokeStaticMethodForList<List<OpenDentBusiness.ProcedureCode>, List<ProcedureCodeInfo>, OpenDentBusiness.ProcedureCode, ProcedureCodeInfo>(Path, "OpenDentBusiness.ProcedureCodes", "GetAllCodes", null);
        }

        public InsPlanInfo GetInsPlanByCarrier(string carrierName)
        {
            return InvokeStaticMethod<InsPlan, InsPlanInfo>(Path, "OpenDentBusiness.InsPlans", "GetByCarrierName", new object[] { carrierName });
        }

        public List<AppointmentInfo> GetAppointmentsStartingWithinPeriod(DateTime from, DateTime to)
        {
            return InvokeStaticMethodForList<List<OpenDentBusiness.Appointment>, List<AppointmentInfo>, OpenDentBusiness.Appointment, AppointmentInfo>(Path,
                "OpenDentBusiness.Appointments", "GetAppointmentsStartingWithinPeriod", new object[] { from, to });
        }
    }
}
