using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
//using System.Threading;
//using System.Threading.Tasks;
using NLog;

namespace CourseStatusMonitorApp
{

	class CourseStatusMonitorApp
	{

        //private string configFile = ".\api_db.config";
        private static Logger _logger = LogManager.GetLogger(typeof(CourseStatusMonitorApp).FullName);
        static void Main(string[] args)
		{
            //1. Load config


            //2. Instantiate Course Status Manager  
            _logger.Info("Checking course status started...");
			CourseBuildingStatusChecker courseStatusManager = new CourseBuildingStatusChecker("F2020");
            /* Test Getting course list */
            //3. If initalized properly - update course status
            if (!courseStatusManager.intializationError)
            {
                courseStatusManager.getCourseBuildingStatus().GetAwaiter();
                while (courseStatusManager.working)
                {
                    Thread.Sleep(100);
                }
            }
            
            _logger.Info("Checking Course Status complete!");
            _ = Console.ReadKey();
		}

        /*
         * For future extension
         * 
		public void loadConfig()
		{
			String jsonConfigFile;
			Console.WriteLine("Loading config from configfile ->" + configFile);
			try{
				jsonConfigFile = File.ReadAllText(configFile);
				Console.WriteLine("Conifg JSON -> " + jsonConfigFile);
			}
			catch(Exception ioException)
			{
				Console.WriteLine("IO Exception caught -> " + ioException.ToString());
				return;
			}
			if (!string.IsNullOrEmpty(jsonConfigFile))
			{
				try
				{
					courseStatusData = Newtonsoft.Json.JsonConvert.DeserializeObject<CourseStatusCheckResults>(jsonConfigFile);
				}
				catch(Exception err)
				{
					string msg = err.Message;
				}
			}
		}
        */
    }
}
