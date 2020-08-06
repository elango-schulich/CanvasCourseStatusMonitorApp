using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseStatusMonitorApp
{
	class ConfigData
	{
		public ConfigData() {}
        public string accessToken { get; set; } = "8296~IZwhqM3pKodvH1LDvr9Wb60EpJzDWkH5I9VdY3YPkbQwrdOlqI089e2p5CiVEjaf";
        public string apiUrl { get; set; } = "https://schulich.test.instructure.com";
        public string databaseName { get; set; } = "CanvasCourseStatuses";
        public string termTableName { get; set; } = "Fall2019";
        public string termStartDate { get; set; } = "2019-09-03";
        public string termEndDate { get; set; } = "2019-12-31";
    }
}
