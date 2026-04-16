// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Linq;
using System.Threading.Tasks;

using Rock.AI.OpenAI.OpenAIApiClient;
using Rock.AI.OpenAI.OpenAIApiClient.Classes.ChatCompletions;

namespace Rock.AI.OpenAI
{
    internal class TextProcessingServiceImplementation : TextProcessingService
    {
        #region Constants

        private const string ModerationInstructions = @"Analyze the message below the `---` line as if you were a moderator.
Score values should be rated between 0 and 1 based on the strength of your determination, with 1 being the highest value.
Text may come various sources, including notes, discussions, feedback, prayer requests and more.
In the case of prayer requests, the content may be asking for prayer about a situation that includes hateful, threatening, self-harm, sexual, or violent content.
In those cases, the content should be rated based on the content of the situation, not the fact they are asking for prayer for a past incident.
It is better to err on the side of caution when rating content, as the goal is to identify content that should then be reviewed by a real person.

- Hate: Content that expresses, incites, or promotes hate based on race, gender, ethnicity, religion, nationality, sexual orientation, disability status, or caste.
- Threat: Harassment content that also includes violence or serious harm towards any target.
- SelfHarm: Content that promotes, encourages, or depicts acts of self-harm, such as suicide, cutting, and eating disorders.
- Sexual: Content meant to arouse sexual excitement, such as the description of sexual activity, or that promotes sexual services (excluding sex education and wellness).
- SexualMinor: Sexual content that includes an individual who is under 18 years old.
- Violent: Content that depicts death, violence, or physical injury.

The response must be only text that makes up a valid JSON object matching the example structure as it will be parsed automatically by machine:

```
{
  ""IsHate"": <true/false>,
  ""HateScore"": <number>,
  ""IsThreat"": <true/false>,
  ""ThreatScore"": <number>,
  ""IsSelfHarm"": <true/false>,
  ""SelfHarmScore"": <number>,
  ""IsSexual"": <true/false>,
  ""SexualScore"": <number>,
  ""IsSexualMinor"": <true/false>,
  ""SexualMinorScore"": <number>,
  ""IsViolent"": <true/false>,
  ""ViolentScore"": <number>
}
```

---
";

        #endregion

#warning FIX ME: This needs to check configuration to see if we have a URL and API Key.
        /// <inheritdoc/>
        public override bool IsAvailable => true;

        /// <inheritdoc/>
        public override async Task<ChatCompletionResponse> GetChatCompletionAsync( ChatCompletionRequest request )
        {
            var aiApi = GetAIApi();

#pragma warning disable CS0618 // Type or member is obsolete
            var chatRequest = new OpenAIChatCompletionsRequest
            {
                Model = "@preset/rock-default",
                Messages =
                [
                    new OpenAIChatCompletionsRequestMessage
                    {
                        Role = OpenAIApiClient.Enums.OpenAIChatMessageRole.User,
                        Content = request.Message,
                    }
                ]
            };
#pragma warning restore CS0618 // Type or member is obsolete

            var response = await aiApi.GetChatCompletions( chatRequest );

            if ( response == null )
            {
                return new ChatCompletionResponseImplementation( "No response from AI API.", false );
            }

            return new ChatCompletionResponseImplementation( response.Choices.FirstOrDefault().Message.Content, true );
        }

        /// <inheritdoc/>
        public override async Task<ModerationResponse> GetModerationAsync( ModerationRequest request )
        {
            var response = await GetChatCompletionAsync( new ChatCompletionRequest
            {
                Message = ModerationInstructions + request.Text,
            } );

            if ( !response.IsSuccessful )
            {
                return new ModerationResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = response.ErrorMessage,
                };
            }

            var data = response.GetText().FromJsonOrNull<ModerationResponse>();

            if ( data == null )
            {
                return new ModerationResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = "Unable to parse moderation response.",
                };
            }

            data.IsSuccessful = true;

            return data;
        }

        /// <summary>
        /// Method to return an OpenAIApi object providing the connection information.
        /// </summary>
        /// <returns></returns>
        private OpenAIApi GetAIApi()
        {
#warning FIX ME: This needs to check configuration to see if we have a URL and API Key.
            var key = "";
            var host = "";

            var api = new OpenAIApi( key, "", host );

            return api;
        }
    }
}
