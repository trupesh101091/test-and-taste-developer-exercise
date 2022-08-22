using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Test_Taste_Console_Application.Constants;
using Test_Taste_Console_Application.Domain.DataTransferObjects;
using Test_Taste_Console_Application.Domain.DataTransferObjects.JsonObjects;
using Test_Taste_Console_Application.Domain.Objects;
using Test_Taste_Console_Application.Domain.Services.Interfaces;
using Test_Taste_Console_Application.Utilities;

namespace Test_Taste_Console_Application.Domain.Services
{
    /// <inheritdoc />
    public class PlanetService : IPlanetService
    {
        private readonly HttpClientService _httpClientService;

        public PlanetService(HttpClientService httpClientService)
        {
            _httpClientService = httpClientService;
        }

        public async Task<IEnumerable<Planet>> GetAllPlanets()
        {
            var allPlanetsWithTheirMoons = new Collection<Planet>();
            Console.WriteLine("Get All Planet details service started!");
            var response = _httpClientService.Client
                .GetAsync(UriPath.GetAllPlanetsWithMoonsQueryParameters)
                .Result;
            Console.WriteLine("Get All Planet details service completed!");
            //If the status code isn't 200-299, then the function returns an empty collection.
            if (!response.IsSuccessStatusCode)
            {
                Logger.Instance.Warn($"{LoggerMessage.GetRequestFailed}{response.StatusCode}");
                return allPlanetsWithTheirMoons;
            }

            var content = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine("Read Plantes details response  process completed..!");

            //The JSON converter uses DTO's, that can be found in the DataTransferObjects folder, to deserialize the response content.
            var results = JsonConvert.DeserializeObject<JsonResult<PlanetDto>>(content);

            //The JSON converter can return a null object. 
            if (results == null) return allPlanetsWithTheirMoons;


            ///Looking at the api result data, there may be more than 10000+ data in future.
            ///If we followed the one by one call API based on Id throug foreach then its very time consuming process and user has to wait to complete it which is not feasible approach.
            ///We have used  Asynchronous programming - parallelism feature of C# to implement this solution because There are numerous benefits to using it, such as create separe thread of each process/task, improved application performance and enhanced responsiveness.
            ///As we know that here We have to make the numerous api call to get the data and count average of Gravity which is long-running operation and we need to make sure that it should not block the execution.
            
            allPlanetsWithTheirMoons = await GetPlanetCollectionWithTheirMoons(results);

            return allPlanetsWithTheirMoons;
        }
 
        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

            for (int i = 0; i < normalizedString.Length; i++)
            {
                char c = normalizedString[i];
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder
                .ToString()
                .Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// This async method used to create the collection of Planet object 
        /// </summary>
        /// <param name="planetDto"></param>
        /// <returns></returns>
        private async Task<Collection<Planet>> GetPlanetCollectionWithTheirMoons(JsonResult<PlanetDto> planetDto)
        {

            Collection<Planet> allPlanetsWithTheirMoons = new Collection<Planet>();
            foreach (var planet in planetDto.Bodies)
            {
                if (planet.Moons != null)
                {

                    ///Start - This commented section is just example we can also use the Parallel.ForEach() feature of C#
                    ///to make the parallel api call.

                    //double total = 0.0f;
                    //var newMoonsCollection = new Collection<MoonDto>();
                    //Parallel.ForEach(planet.Moons, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, index =>
                    //{
                    //    var response = _httpClientService.Client.GetAsync(UriPath.GetMoonByIdQueryParameters + index.URLId).Result;
                    //    var moonContent = response.Content.ReadAsStringAsync().Result;
                    //    var moonDTO = JsonConvert.DeserializeObject<MoonDto>(moonContent);
                    //    total += moonDTO.Gravity;
                    //    newMoonsCollection.Add(moonDTO);
                    //});
                    ///End
                    
                    Console.WriteLine("Moon Data Api Call IN progress");
                    List<Task> tasks = await RequestForMoonDetailsWithMoonIdAysnc(planet.Moons);
                    Console.WriteLine("Moon Data Api Call Completed..");

                    Console.WriteLine("Read each api call task response in progress...");
                    Task moonDtoCollection = Task.Factory.StartNew(() => CreateMoonDtoCollectionByReadingMoonApiResponse(tasks));
                    moonDtoCollection.Wait();
                    Console.WriteLine("Read each api call task response process completed...");

                    planet.Moons = ((Task<Collection<MoonDto>>) moonDtoCollection).Result;

                    Console.WriteLine("Average Gravity calculation in progress...");
                    planet.AvgMoonGravity = await CalculateAverageGravity(planet.Moons);
                    Console.WriteLine("Average Gravity calculation is completed...");
                }

                allPlanetsWithTheirMoons.Add(new Planet(planet));
            }
            Session.Data = allPlanetsWithTheirMoons;
            return allPlanetsWithTheirMoons;
        }

        /// <summary>
        /// Create the lisf the TASKs for each api call.
        /// </summary>
        /// <param name="Moons"></param>
        /// <returns></returns>
        private async Task<List<Task>> RequestForMoonDetailsWithMoonIdAysnc(ICollection<MoonDto> Moons)
        {
            //var newMoonsCollection = new Collection<MoonDto>();
            List<Task> tasks = new List<Task>();

            foreach (var moon in Moons)
            {
                async Task<string> func()
                {
                    var response = await _httpClientService.Client.GetAsync(UriPath.GetMoonByIdQueryParameters + moon.URLId);
                    return await response.Content.ReadAsStringAsync();
                }

                tasks.Add(func());
            }
            await Task.WhenAll(tasks);
            Task.WaitAll();
            
            return tasks;
        }

        /// <summary>
        /// This method will read the api result and create the MoonDto Object.
        /// </summary>
        /// <param name="tasks"></param>
        /// <returns></returns>
        private Collection<MoonDto> CreateMoonDtoCollectionByReadingMoonApiResponse(List<Task> tasks)
        {
            var newMoonsCollection = new Collection<MoonDto>();
            foreach (var t in tasks)
            {
                if (t.IsCompletedSuccessfully)
                {
                    var postResponse = ((Task<string>)t).Result; //t.Result would be okay too.
                    MoonDto moonData = JsonConvert.DeserializeObject<MoonDto>(postResponse);
                    newMoonsCollection.Add(moonData);
                }
                else
                {
                    //Implement the process if any api call is failed.
                    //We can also get the how many calls are failed and get the details of each failed api's here.
                }
            }
            return newMoonsCollection;
        }

        /// <summary>
        /// This Async used to calculate the average of Gravity.
        /// </summary>
        /// <param name="moonDto"></param>
        /// <returns></returns>
        private async Task<float> CalculateAverageGravity(ICollection<MoonDto> moonDto)
        {
            try
            {
                float avgGravity = 0.0f;
                float total = 0.0f;

                await Task.Run(() => {
                    foreach (var moon in moonDto)
                    {
                        total += moon.Gravity;
                    }
                    avgGravity = total / moonDto.Count;
                });
                
                return avgGravity;
            }
            catch(ArithmeticException _arithmeticException)
            {
                Console.WriteLine(_arithmeticException.Message);
                throw _arithmeticException;
            }
        }
    }
}
