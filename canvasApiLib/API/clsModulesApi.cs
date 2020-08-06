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
    /// https://canvas.instructure.com/doc/api/modules.html#method.context_modules_api.index
    public class clsModulesApi : clsHttpMethods
	{
		////NLog
		private static Logger _logger = LogManager.GetLogger(typeof(clsModulesApi).FullName);

        /// <summary>
        /// Returns the list of modules of a specific course
        /// https://canvas.instructure.com/doc/api/modules.html#method.context_modules_api.index
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="baseUrl"></param>
        /// <param name="canvasCourseId"></param>
        /// <returns>Returns a json object representing a course object: https://canvas.instructure.com/doc/api/modules.html#Module, throws exception if successful status code is not received </returns>
        public static async Task<dynamic> listCourseModules(string accessToken, string baseUrl, string canvasCourseId, long sisCourseId = 0)
		{
			string rval = string.Empty;
			string urlCommand = "/api/v1/courses/:id/modules/?per_page=30";

			//if we have a valid sis course id, ask canvas to find the course using that identifier
			//  this will be done when we look for an existing course during the cloning request
			urlCommand = (sisCourseId > 0) ? urlCommand.Replace(":id", "sis_course_id:" + sisCourseId.ToString()) : urlCommand.Replace(":id", canvasCourseId.ToString());
			_logger.Debug("[listCourseModules] " + urlCommand);
			
			using (HttpResponseMessage response = await httpGET(baseUrl, urlCommand, accessToken))
			{
				rval = await response.Content.ReadAsStringAsync();
				_logger.Debug("listCourseModules API Response: [" + rval + "]");

				if (!response.IsSuccessStatusCode || (rval.Contains("errors") && rval.Contains("message")))
				{
					string msg = "[listCourseModules]:[" + urlCommand + "] returned status[" + response.StatusCode + "]: " + response.ReasonPhrase;
					_logger.Error(msg);
					throw new HttpRequestException(rval);
				}
			}

			return Newtonsoft.Json.JsonConvert.DeserializeObject(rval);
		}



        /// <summary>  Elango Vanangamudi
        /// Get the list of module items of a specific module
        /// https://canvas.instructure.com/doc/api/modules.html#ModuleItem
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="baseUrl"></param>
        /// <param name="moduleId"></param>
        /// <param name="MigrationContextId"></param>
        /// <returns>Returns a json object representing a course object: https://canvas.instructure.com/doc/api/modules.html#method.context_module_items_api.index, throws exception if successful status code is not received </returns>
        public static async Task<dynamic> listModuleItems(string accessToken, string baseUrl, string canvasCourseId, string canvasModuleId)
		{
			string rval = string.Empty;
			string urlCommand = " /api/v1/courses/:course_id/modules/:module_id/items";

			//if we have a valid sis course id, ask canvas to find the course using that identifier
			//  this will be done when we look for an existing course during the cloning request
			urlCommand = urlCommand.Replace(":course_id", canvasCourseId.ToString());
			urlCommand = urlCommand.Replace(":module_id", canvasModuleId.ToString());
			_logger.Debug("[listModuleItems] " + urlCommand);

			using (HttpResponseMessage response = await httpGET(baseUrl, urlCommand, accessToken))
			{
				rval = await response.Content.ReadAsStringAsync();
				_logger.Debug("listModuleItems API Response: [" + rval + "]");

				if (!response.IsSuccessStatusCode || (rval.Contains("errors") && rval.Contains("message")))
				{
					string msg = "[listModuleItems]:[" + urlCommand + "] returned status[" + response.StatusCode + "]: " + response.ReasonPhrase;
					_logger.Error(msg);
					throw new HttpRequestException(rval);
				}
			}

			return Newtonsoft.Json.JsonConvert.DeserializeObject(rval);
		}
        
	}
}
