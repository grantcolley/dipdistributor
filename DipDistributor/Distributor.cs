﻿//-----------------------------------------------------------------------
// <copyright file="Distributor.cs" company="Development In Progress Ltd">
//     Copyright © 2017. All rights reserved.
// </copyright>
// <author>Grant Colley</author>
//-----------------------------------------------------------------------

using DipRunner;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DipDistributor
{
    public class Distributor
    {
        private string logFile;
        private HttpClient logClient;
        private string dependencyDirectory;

        public async Task<Step> RunAsync(Step step)
        {
            if (step == null)
            {
                throw new Exception(CreateMessage($"Step is null. Machine Name: {Environment.MachineName}"));
            }

            if (string.IsNullOrWhiteSpace(step.RunName))
            {
                throw new Exception(CreateMessage(step, "Run Name is missing."));
            }

            if (string.IsNullOrWhiteSpace(step.StepName))
            {
                throw new Exception(CreateMessage(step, "Step Name is missing."));
            }

            if (string.IsNullOrWhiteSpace(step.LogUrl))
            {
                throw new Exception(CreateMessage(step, "Log url is missing."));
            }

            if (string.IsNullOrWhiteSpace(step.DependencyUrl))
            {
                throw new Exception(CreateMessage(step, "Dependency url is missing."));
            }

            if (step.Urls == null
                || step.Urls.Count() == 0)
            {
                throw new Exception(CreateMessage(step, "Url is missing."));
            }

            logFile = step.LogFileLocation ?? "DistributorLog.txt";

            logClient = new HttpClient();
            logClient.DefaultRequestHeaders.Accept.Clear();
            logClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            logClient.BaseAddress = new Uri(step.LogUrl);

            return await ProcessStep(step).ConfigureAwait(false);
        }

        private async Task<Step> ProcessStep(Step step)
        {
            var initialised = await InitialiseStepAsync(step).ConfigureAwait(false);

            if (initialised)
            {
                var stepSuccessful = await RunStepAsync(step).ConfigureAwait(false);

                if (stepSuccessful)
                {
                    var subStepsSuccessful = await RunSubStepsAsync(step).ConfigureAwait(false);

                    if (subStepsSuccessful)
                    {
                        var completed = await CompleteStepAsync(step).ConfigureAwait(false);
                    }
                }
            }

            return step;
        }

        private async Task<bool> InitialiseStepAsync(Step step)
        {
            try
            {               
                step.Status = StepStatus.Initialise;

                await Log(step);

                dependencyDirectory = Path.Combine(Directory.GetCurrentDirectory(), step.RunName);
                if (!Directory.Exists(dependencyDirectory))
                {
                    await Log(step, $"Create directory {dependencyDirectory}");

                    Directory.CreateDirectory(dependencyDirectory);
                }

                return await DownloadDependenciesAsync(step).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Log(step, ex.ToString());
                return false;
            }
        }

        private async Task<bool> DownloadDependenciesAsync(Step step)
        {
            try
            {
                if (step.Dependencies == null
                    || step.Dependencies.Length == 0)
                {
                    await Log(step, "No dependencies");
                    return true;
                }

                await Log(step, "Downloading dependencies...");

                var client = new HttpClient() { MaxResponseContentBufferSize = 1000000 };
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var dependencies = new List<string>(step.Dependencies);

                IEnumerable<Task<bool>> downloadQuery = from dependency
                                                        in step.Dependencies
                                                        select DownloadDependencyAsync(step, client, step.DependencyUrl, dependency);

                Task<bool>[] downloads = downloadQuery.ToArray();

                var results = await Task.WhenAll(downloads);

                return results.All(r => r == true);
            }
            catch (Exception ex)
            {
                await Log(step, ex.ToString());
                return false;
            }
        }

        private async Task<bool> DownloadDependencyAsync(Step step, HttpClient client, string dependencyUri, string filePath)
        {
            try
            {
                var uri = new Uri($"{dependencyUri}?file={filePath}");
                var stream = await client.GetStreamAsync(uri);

                var fileName = filePath.Split('\\');
                var fullFileName = Path.Combine(dependencyDirectory, fileName[fileName.Length - 1]);

                if(File.Exists(fullFileName))
                {
                    await Log(step, $"File already exists: {fullFileName}");
                    return true;
                }

                using (var file = File.Create(fullFileName))
                {
                    byte[] buffer = new byte[8 * 1024];
                    int len;
                    while ((len = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        file.Write(buffer, 0, len);
                    }

                    stream.Dispose();
                    stream = null;
                }

                await Log(step, $"Downloaded: {fullFileName}");
                return true;
            }
            catch(Exception ex)
            {
                await Log(step, ex.ToString());
                return false;
            }
        }

        private async Task<bool> RunStepAsync(Step step)
        {
            try
            {
                step.Status = StepStatus.InProgress;

                await Log(step);

                if (string.IsNullOrWhiteSpace(step.TargetAssembly))
                {
                    await Log(step, "TargetAssembly is missing.");
                    return true;
                }

                if (string.IsNullOrWhiteSpace(step.TargetType))
                {
                    await Log(step, "TargetType is missing.");
                    return true;
                }

                var dependencies = GetDependencyAssemblyNames(step);

                var assemblyLoader = new AssemblyLoader(dependencyDirectory, dependencies);
                var assembly = assemblyLoader.LoadFromAssemblyPath(Path.Combine(dependencyDirectory, step.TargetAssembly));

                var type = assembly.GetType(step.TargetType);
                dynamic obj = Activator.CreateInstance(type);

                await Log(step, $"Before {step.TargetType}.RunAsync() --> {step?.Payload}");

                var result = await obj.RunAsync(step);

                await Log(step, $"Before {step.TargetType}.RunAsync() --> {step?.Payload}");

                return true;
            }
            catch (Exception ex)
            {
                await Log(step, ex.ToString());
                return false;
            }
        }

        private async Task<bool> RunSubStepsAsync(Step step)
        {
            try
            {
                if (step.SubSteps == null
                    || !step.SubSteps.Any())
                {
                    await Log(step, "No sub steps");
                    return true;
                }

                await Log(step, "Running sub steps");

                IEnumerable<Task<Step>> subStepQuery = from subStep in step.SubSteps select DistributeStep(subStep);

                Task<Step>[] subSteps = subStepQuery.ToArray();

                var results = await Task.WhenAll(subSteps);
                                
                return results.All(r => r.Status == StepStatus.Complete);
            }
            catch(Exception ex)
            {
                await Log(step, ex.ToString());
                return false;
            }
        }

        private async Task<bool> CompleteStepAsync(Step step)
        {
            try
            {
                step.Status = StepStatus.Complete;

                if (step.TransitionSteps == null
                    || !step.TransitionSteps.Any())
                {
                    await Log(step, "No transition steps");
                    return true;
                }

                await Log(step, "Running transition steps");

                IEnumerable<Task<Step>> transitionStepQuery = from transition in step.TransitionSteps select DistributeStep(transition);

                Task<Step>[] transitionSteps = transitionStepQuery.ToArray();

                var results = await Task.WhenAll(transitionSteps);

                return results.All(r => r.Status == StepStatus.Complete);
            }
            catch (Exception ex)
            {
                await Log(step, ex.ToString());
                return false;
            }
        }

        private async Task<Step> DistributeStep(Step step)
        {
            var jsonContent = JsonConvert.SerializeObject(step);
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await client.PostAsync(step.Urls[0], new StringContent(jsonContent, Encoding.UTF8, "application/json"));

            var content = await response.Content.ReadAsStringAsync();
            var responseStep = JsonConvert.DeserializeObject<Step>(content);

            return responseStep;
        }

        private IList<string> GetDependencyAssemblyNames(Step step)
        {
            var dependencies = new List<string>();
            foreach (string filePath in step.Dependencies)
            {
                var filePathSplit = filePath.Split('\\');
                var fileName = filePathSplit[filePathSplit.Length - 1];
                var name = fileName.Substring(0, fileName.LastIndexOf('.'));
                dependencies.Add(name);
            }

            return dependencies;
        }

        private async Task Log(Step step, string message = "")
        {
            var logMessage = CreateMessage(step, message);
            await logClient.PostAsync("api/distributor/log", new StringContent(JsonConvert.SerializeObject(logMessage), Encoding.UTF8, "application/json"));
        }

        private string CreateMessage(string message)
        {
            return CreateMessage(new Step(), message);
        }

        private string CreateMessage(Step step, string message)
        {
            var logMessage = $"{DateTime.Now}   {Environment.MachineName}   RunId: {step.RunId}; Run Name: {step.RunName}; StepId: {step.StepId}; Step Name: {step.StepName}; Step Status: {step.Status}";

            if (!string.IsNullOrWhiteSpace(message))
            {
                logMessage += $"; Message: {message}";
            }

            return logMessage;
        }
    }
}
