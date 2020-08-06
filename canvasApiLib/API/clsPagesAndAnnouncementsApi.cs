using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using NLog;

using canvasApiLib.Base;

namespace canvasApiLib.API
{
    /// <summary>
    /// Implementation of the Canvas Pages and Annoucements API definition
    /// /// </summary> found here:
    /// https://canvas.instructure.com/doc/api/pages.html
    /// https://canvas.instructure.com/doc/api/announcements.html
    public class clsPagesAndAnnoucementsApi : clsHttpMethods
	{
		////NLog
		private static Logger _logger = LogManager.GetLogger(typeof(clsPagesAndAnnoucementsApi).FullName);

        /// <summary>
        /// Returns the list of modules of a specific course
        /// https://canvas.instructure.com/doc/api/pages.html#method.wiki_pages_api.index
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="baseUrl"></param>
        /// <param name="canvasCourseId"></param>
        /// <returns>Returns a json object representing a course object: https://canvas.instructure.com/doc/api/pages.html, throws exception if successful status code is not received </returns>
        public static async Task<dynamic> listCoursePages(string accessToken, string baseUrl, string canvasCourseId, long sisCourseId = 0)
		{
			string rval = string.Empty;
			string urlCommand = "/api/v1/courses/:id/pages";

			//if we have a valid sis course id, ask canvas to find the course using that identifier
			//  this will be done when we look for an existing course during the cloning request
			urlCommand = (sisCourseId > 0) ? urlCommand.Replace(":id", "sis_course_id:" + sisCourseId.ToString()) : urlCommand.Replace(":id", canvasCourseId.ToString());
			_logger.Debug("[listCoursePages] " + urlCommand);
			
			using (HttpResponseMessage response = await httpGET(baseUrl, urlCommand, accessToken))
			{
				rval = await response.Content.ReadAsStringAsync();
				_logger.Debug("listCoursePages API Response: [" + rval + "]");

				if (!response.IsSuccessStatusCode || (rval.Contains("errors") && rval.Contains("message")))
				{
					string msg = "[listCoursePages]:[" + urlCommand + "] returned status[" + response.StatusCode + "]: " + response.ReasonPhrase;
					_logger.Error(msg);
					throw new HttpRequestException(rval);
				}
			}

			return Newtonsoft.Json.JsonConvert.DeserializeObject(rval);
		}



        /// <summary>  Elango Vanangamudi
        /// Get the list of announcements
        /// https://canvas.instructure.com/doc/api/modules.html#ModuleItem
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="baseUrl"></param>
        /// <param name="canvasCourseId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns>Returns a json object representing a course object: https://canvas.instructure.com/doc/api/modules.html#method.context_module_items_api.index, throws exception if successful status code is not received </returns>
        public static async Task<dynamic> listCourseAnnouncements(string accessToken, string baseUrl, string canvasCourseId, string startDate, string endDate)
		{
			string rval = string.Empty;
			string urlCommand = "/api/v1/announcements?context_codes[]=course_:id&start_date=:start_date&end_date=:end_date&active_only=true";

			//if we have a valid sis course id, ask canvas to find the course using that identifier
			//  this will be done when we look for an existing course during the cloning request
			urlCommand = urlCommand.Replace(":id", canvasCourseId.ToString());
            urlCommand = urlCommand.Replace(":start_date", startDate.ToString());
            urlCommand = urlCommand.Replace(":end_date", endDate.ToString()); 
			_logger.Debug("[listCourseAnnouncement] " + urlCommand);
            //Console.WriteLine(" list annoucements url->" + urlCommand);
			using (HttpResponseMessage response = await httpGET(baseUrl, urlCommand, accessToken))
			{
				rval = await response.Content.ReadAsStringAsync();
				_logger.Debug("listCourseAnnouncement API Response: [" + rval + "]");

                if (!response.IsSuccessStatusCode || (rval.Contains("errors") && rval.Contains("message"))) 
				{
					string msg = "[listCourseAnnouncement]:[" + urlCommand + "] returned status[" + response.StatusCode + "]: " + response.ReasonPhrase;
					_logger.Error(msg);
					throw new HttpRequestException(rval);
				}
			}

			return Newtonsoft.Json.JsonConvert.DeserializeObject(rval);
		}
        
	}
}
