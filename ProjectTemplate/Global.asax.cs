using System;
using System.Web;

namespace ProjectTemplate
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // Temporarily disabled Web API startup to prevent parser/startup crash.
            // ASMX services (ProjectServices.asmx) do not require WebApiConfig.
        }
    }
}
