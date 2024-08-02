namespace DynamoAssistant
{
    /// <summary>
    /// Class define the API key storage for the OpenAI project
    /// TODO: implement environment variable to store the API key
    /// </summary>
    internal class APIKeyStorage
    {
        /// <summary>
        /// Return the OpenAI project API key
        /// </summary>
        public static string APIKey => "OpenAI_Project_API_Key";
    }
}
