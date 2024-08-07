using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Dynamo.Core;
using Dynamo.Extensions;
using Dynamo.Models;
using Dynamo.UI.Commands;
using Dynamo.ViewModels;
using NAudio.Wave;
using OpenAI.Assistants;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using System.Text.RegularExpressions;

namespace DynamoAssistant
{
    public class DynamoAssistantWindowViewModel : NotificationObject, IDisposable
    {
        #region private fields
        private readonly ReadyParams readyParams;

        // Chat GPT related fields
        private readonly ChatClient chatGPTClient;

        // The name of the assistant
        private readonly string AssistantName = "Gen-AI assistant:\n";

        //private readonly Conversation conversation;
        private static readonly string apikey = APIKeyStorage.APIKey;

        // Chat GPT pre instruction fields
        // A set of instructions to prepare GPT to describe Dynamo graph better
        private const string DescribePreInstruction = "Given a JSON file representing a Dynamo for Revit project, perform a comprehensive analysis focusing on the graph's node structure. Your tasks include:\r\n\r\nReview Node Connections: Ensure each node is connected correctly according to Dynamo's expected data types and functionalities. Identify any instances where inputs may be receiving incorrect data types or where outputs are not utilized efficiently.\r\n\r\nData Type Validation: For each node input and output, validate that the data types are compatible with their intended functions. Highlight mismatches, such as a string data type connected to a numeric input without appropriate conversion.";

        // A set of instructions to prepare GPT to optimize Dynamo graph better
        private const string OptimizePreInstruction = "Given a JSON file representing a Dynamo for Revit project, perform a comprehensive analysis focusing on the graph's node structure. Your tasks include:\r\n\r\nIdentify Unnecessary Nodes: Detect nodes that do not contribute to the final output or create redundant processes within the graph. This includes nodes with default values that never change or intermediary nodes that could be bypassed without altering the graph's outcome.\r\n\r\nOptimization Recommendations: Based on your analysis, recommend specific changes to the node structure. This might involve reordering nodes for logical flow, changing node types for efficiency, or altering connections to ensure data type compatibility.\r\n\r\nUpdate JSON Structure: Apply the optimization recommendations to the JSON file. Directly modify the \"Nodes\" and \"Connectors\" sections to reflect the optimized graph layout. Ensure that all other elements of the JSON file, such as \"Uuid\", \"Description\", \"ElementResolver\", and metadata, remain unchanged to preserve the file's integrity and additional context.\r\n\r\nOutput an Optimized JSON: Provide a revised JSON file, focusing exclusively on an updated node structure that reflects your analysis and optimizations. This file should retain all original details except for the modifications to nodes and their connections to address identified issues and enhance efficiency.";

        /// <summary>
        /// User input to the Gen-AI assistant
        /// </summary>
        private string userInput;

        /// <summary>
        /// Response from backend service
        /// </summary>
        private string response;
        #endregion

        /// <summary>
        /// User input to the Gen-AI assistant
        /// </summary>
        public string UserInput
        {
            get { return userInput; }
            set
            {
                if (value != null)
                {
                    // Set the value of the MessageText property
                    userInput = value;
                    // Raise the PropertyChanged event
                    RaisePropertyChanged(nameof(UserInput));
                }
            }
        }

        internal DynamoViewModel dynamoViewModel;

        /// <summary>
        /// If user prefers voice response over text
        /// </summary>
        internal bool IsVoicePreferred = false;

        /// <summary>
        /// Dynamo Model getter
        /// </summary>
        internal DynamoModel DynamoModel => dynamoViewModel.Model;

        /// <summary>
        /// Is Gopilot waiting for input, this boolean dominates certain UX aspects
        /// </summary>
        public bool IsWaitingForInput = true;

        public ObservableCollection<string> Messages{ get; set; } = new ObservableCollection<string>();

        /// <summary>
        /// Constructor for the DynamoAssistantWindowViewModel
        /// </summary>
        /// <param name="p"></param>
        public DynamoAssistantWindowViewModel(ReadyParams p)
        {
            readyParams = p;

            // Create a ChatGPTClient instance with the API key
            chatGPTClient = new(model: "gpt-4o", apikey);

            // Adjust this value for more or less "creativity" in the response
            // conversation.RequestParameters.Temperature = 0.1;

            // Display a welcome message
            Messages.Add(AssistantName + "Welcome to Dynamo world and ask me anything to get started!\n");
        }

        /// <summary>
        /// Send user message to ChatGPT and display the response
        /// </summary>
        /// <param name="msg">User input</param>
        internal async void SendMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            IsWaitingForInput = false;

            // Display user message first
            Messages.Add("You:\n" + msg + "\n");
            // Developer can make the switch here below
            // await SendMessageToGPT(msg);
            await SendMessageToAssistant(msg);

            // Display the chatbot's response   
            Messages.Add(AssistantName + response + "\n");

            // If user prefers voice response, convert the response to speech
            if (IsVoicePreferred)
            {
                await Task.Run(() => OpenAITextToVoice(response));
            }

            // TODO: Add more logic to handle different responses which would include Python node creation
            var responseToLower = response.ToLower();
            if (responseToLower.Contains("python script") || responseToLower.Contains("python node"))
            {
                CreatePythonNode(response);
            }
            IsWaitingForInput = true;
        }

        internal async Task SendMessageToGPT(string msg)
        {
            // Send the user's input to the ChatGPT client and start to stream the response
            // Single chat completion
            ChatCompletion completion = await chatGPTClient.CompleteChatAsync(msg);
            response = completion.ToString();
        }

        internal async Task SendMessageToAssistant(string msg)
        {
            // Display the chatbot's response
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            AssistantClient client = new(apikey);
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            var assistant = client.GetAssistant("asst_J8PSA1asQDqEGluCGMykzJhJ").Value;
            //Console.WriteLine(assistant.Name + assistant.Description + assistant.Temperature);

            // Create a thread with an initial user message and run it.
            ThreadCreationOptions threadOptions = new()
            {
                InitialMessages = { msg }
            };

            ThreadRun run = client.CreateThreadAndRun(assistant.Id, threadOptions);

            // Poll the run until it is no longer queued or in progress.
            while (!run.Status.IsTerminal)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                run = await client.GetRunAsync(run.ThreadId, run.Id);
            }

            // With the run complete, list the messages and display their content
            if (run.Status == RunStatus.Completed)
            {
                PageCollection<ThreadMessage> messagePages
                    = client.GetMessages(run.ThreadId, new MessageCollectionOptions() { Order = ListOrder.OldestFirst });
                IEnumerable<ThreadMessage> messages = messagePages.GetAllValues();

                foreach (ThreadMessage message in messages)
                {
                    if (!message.Role.ToString().ToUpper().Equals("User"))
                    {
                        foreach (MessageContent contentItem in message.Content)
                        {
                            // Set response and clean up resources references
                            Regex regex = new Regex("【.*?†source】");
                            response = regex.Replace(contentItem.Text, "");
                        }
                    }
                }
            }
            else
            {
                throw new NotImplementedException(run.Status.ToString());
            }
        }

        /// <summary>
        /// Function to convert input text to speech using OpenAI API
        /// </summary>
        /// <param name="input"></param>
        internal async Task OpenAITextToVoice(string input)
        {
            AudioClient client = new(model: "tts-1", apikey);
            BinaryData speech = client.GenerateSpeechFromText(input, GeneratedSpeechVoice.Echo);
            var fileName = $"{Guid.NewGuid()}.mp3";
            using FileStream stream = File.OpenWrite(fileName);
            speech.ToStream().CopyTo(stream);
            stream.Close();

            using (var audioFile = new AudioFileReader(fileName))
            using (var outputDevice = new WaveOutEvent())
            {
                outputDevice.Init(audioFile);
                outputDevice.Play();
                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        internal async void DescribeGraph()
        {
            // Set Dynamo file location
            string filePath = readyParams.CurrentWorkspaceModel.FileName;
            if (string.IsNullOrEmpty(filePath))
            {
                // Alternatively, export Json from current workspace model to continue
                Messages.Add(AssistantName + "Please save the workspace first.\n");
                return;
            }

            // Read the file
            // TODO: Add more logic to remove the graph thumbnail from the JSON file
            string jsonData = File.ReadAllText(filePath);

            var msg = "This is my Dynamo project JSON structure.\n" + jsonData;

            // Send the user's input to the ChatGPT API and receive a response
            ChatCompletion completion = await chatGPTClient.CompleteChatAsync(DescribePreInstruction + msg);
            var response = completion.ToString();

            // Display the chatbot's graph description
            Messages.Add(AssistantName + response + "\n");
        }

        internal async void OptimizeGraph()
        {
            // Set Dynamo file location
            string filePath = readyParams.CurrentWorkspaceModel.FileName;
            if (string.IsNullOrEmpty(filePath))
            {
                // Alternatively, export Json from current workspace model to continue
                Messages.Add(AssistantName + "Please save the workspace first.\n");
                return;
            }

            //Read the file 
            string jsonData = File.ReadAllText(filePath);

            var msg = "This is my Dynamo project JSON structure." + jsonData;

            // Send the user's input to the ChatGPT API and receive a response
            ChatCompletion completion = await chatGPTClient.CompleteChatAsync(OptimizePreInstruction + msg);
            var response = completion.ToString();

            // This file overwrite the original file, please be careful
            // File.WriteAllText(filePath, response);
            // Display the chatbot's response
            Messages.Add(AssistantName + response + "\n");
        }

        /// <summary>
        /// returns the user what's new in latest version of Dynamo
        /// </summary>
        internal async void WhatsNew()
        {
            // Send the user's input to the ChatGPT API and receive a response
            ChatCompletion completion = await chatGPTClient.CompleteChatAsync("What's new in Dynamo 3.0?");
            var response = completion.ToString();
            // Display the chatbot's response
            Messages.Add(AssistantName + response + "\n");
        }

        /// <summary>
        /// Create a new annotation
        /// </summary>
        internal void MakeNote()
        {
            // create a Dynamo note example
            CreateNote((new Guid()).ToString(), "This is a sample Note.", 0, 0, true);
        }

        /// <summary>
        /// Create a group for the selected node(s) with empty description
        /// </summary>
        internal void MakeGroup()
        {
            // create a Dynamo group example
            DynamoModel.ExecuteCommand(new DynamoModel.CreateAnnotationCommand(new Guid(), "This is a sample Group.", string.Empty, 0, 0, true));
        }

        /// <summary>
        /// Create a python node in Dynamo, use a beta Nuget package for this
        /// </summary>
        /// <param name="response"></param>
        internal void CreatePythonNode(string response)
        {
            string pythonScript = string.Empty;
            if (response.Contains("```python"))
            {
                pythonScript = response.Split("```python")[1];
                if (pythonScript.Contains("```"))
                {
                    pythonScript = pythonScript.Split("```")[0];
                }
            }
            else return;

            var pythonNode = new PythonNodeModels.PythonNode
            {
                Script = pythonScript
            };
            DynamoModel.ExecuteCommand(new DynamoModel.CreateNodeCommand(pythonNode, 0, 0, true, false));
            Messages.Add(AssistantName + "The Python node including the code above has been created for you!\n");
        }

        internal void CreateNote(string nodeId, string noteText, double x, double y, bool defaultPosition)
        {
            DynamoModel.ExecuteCommand(new DynamoModel.CreateNoteCommand(nodeId, noteText, x, y, defaultPosition));
            Messages.Add(AssistantName + "Your note has been created!\n");
        }

        private DelegateCommand enterCommand;

        public ICommand EnterCommand
        {
            get
            {
                enterCommand ??= new DelegateCommand(Enter);

                return enterCommand;
            }
        }

        /// <summary>
        /// Enter/Send button handler
        /// </summary>
        /// <param name="commandParameter"></param>
        private void Enter(object commandParameter)
        {
            SendMessage(commandParameter as string);
            // Raise event to update the UI and clear the input box
            UserInput = string.Empty;
        }

        /// <summary>
        /// Dispose function
        /// </summary>
        public void Dispose()
        {
            // Do nothing
        }
    }
}
