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
    /// Implementation of the Canvas Modules API definition
    /// /// </summary> found here:
    /// https://canvas.instructure.com/doc/api/assignments.html
    public class clsAssignmentsApi : clsHttpMethods
	{
		////NLog
		private static Logger _logger = LogManager.GetLogger(typeof(clsAssignmentsApi).FullName);

        /// <summary>
        /// Returns the list of assignments of a specific course
        /// 
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="baseUrl"></param>
        /// <param name="canvasCourseId"></param>
        /// <returns>Returns a json object representing a course object:https://canvas.instructure.com/doc/api/assignments.html, throws exception if successful status code is not received </returns>
        public static async Task<dynamic> listCourseAssignments(string accessToken, string baseUrl, string canvasCourseId, long sisCourseId = 0)
		{
			string rval = string.Empty;
			string urlCommand = "/api/v1/courses/:id/assignments?per_page=20";

			//if we have a valid sis course id, ask canvas to find the course using that identifier
			//  this will be done when we look for an existing course during the cloning request
			urlCommand = (sisCourseId > 0) ? urlCommand.Replace(":id", "sis_course_id:" + sisCourseId.ToString()) : urlCommand.Replace(":id", canvasCourseId.ToString());
			_logger.Debug("[listCourseAssignments] " + urlCommand);
			
			using (HttpResponseMessage response = await httpGET(baseUrl, urlCommand, accessToken))
			{
				rval = await response.Content.ReadAsStringAsync();
				_logger.Debug("listCourseAssignments API Response: [" + rval + "]");

				if (!response.IsSuccessStatusCode || (rval.Contains("errors") && rval.Contains("message")))
				{
					string msg = "[listCourseAssignments]:[" + urlCommand + "] returned status[" + response.StatusCode + "]: " + response.ReasonPhrase;
					_logger.Error(msg);
					throw new HttpRequestException(rval);
				}
			}

			return Newtonsoft.Json.JsonConvert.DeserializeObject(rval);
		}

        /// <summary>
        /// Returns the list of assignment groups of a specific course
        /// 
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="baseUrl"></param>
        /// <param name="canvasCourseId"></param>
        /// <returns>Returns a json object representing a course object:https://canvas.instructure.com/doc/api/assignments.html, throws exception if successful status code is not received </returns>
        public static async Task<dynamic> listCourseAssignmentGroups(string accessToken, string baseUrl, string canvasCourseId, long sisCourseId = 0)
        {
            string rval = string.Empty;
            string urlCommand = "/api/v1/courses/:id/assignment_groups";

            //if we have a valid sis course id, ask canvas to find the course using that identifier
            //  this will be done when we look for an existing course during the cloning request
            urlCommand = (sisCourseId > 0) ? urlCommand.Replace(":id", "sis_course_id:" + sisCourseId.ToString()) : urlCommand.Replace(":id", canvasCourseId.ToString());
            _logger.Debug("[listCourseAssignmentGroups] " + urlCommand);

            using (HttpResponseMessage response = await httpGET(baseUrl, urlCommand, accessToken))
            {
                rval = await response.Content.ReadAsStringAsync();
                _logger.Debug("listCourseAssignmentGroups API Response: [" + rval + "]");

                if (!response.IsSuccessStatusCode || (rval.Contains("errors") && rval.Contains("message")))
                {
                    string msg = "[listCourseAssignmentGroups]:[" + urlCommand + "] returned status[" + response.StatusCode + "]: " + response.ReasonPhrase;
                    _logger.Error(msg);
                    throw new HttpRequestException(rval);
                }
            }

            return Newtonsoft.Json.JsonConvert.DeserializeObject(rval);
        }




    }
}
