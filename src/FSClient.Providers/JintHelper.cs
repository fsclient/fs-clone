namespace FSClient.Providers
{
    using System;
    using System.Text;

    using Jint;
    using Jint.Native;
    using Jint.Runtime;
    using Jint.Runtime.Interop;

    public static class JintHelper
    {
        public static Engine SetupBrowserFunctions(this Engine engine)
        {
            return engine
                .SetValue("atob", new ClrFunctionInstance(engine, "atob", AtoB, 1))
                .SetValue("btoa", new ClrFunctionInstance(engine, "btoa", BtoA, 1));
        }

        private static JsValue AtoB(JsValue thisObject, JsValue[] arguments)
        {
            var argument = arguments.At(0);
            var inputString = TypeConverter.ToString(argument);
            if (inputString.Length % 4 != 0)
            {
                inputString += new string('=', 4 - (inputString.Length % 4));
            }
            
            var bytes = Convert.FromBase64String(inputString);
            return Encoding.GetEncoding(28591).GetString(bytes);
        }

        private static JsValue BtoA(JsValue thisObject, JsValue[] arguments)
        {
            var argument = arguments.At(0);
            var inputString = TypeConverter.ToString(argument);
            var bytes = Encoding.GetEncoding(28591).GetBytes(inputString);
            return Convert.ToBase64String(bytes);
        }
    }
}
