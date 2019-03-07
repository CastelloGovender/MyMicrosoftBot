// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;

namespace EchoBot1
{

    public class EchoBot1Bot : IBot
    {
        private const string WelcomeText = "Welcome to EchoBot. This bot will introduce multiple turns using prompts.  Type anything to get started.";
        private readonly EchoBot1Accessors _accessors;
        private readonly ILogger _logger;
        private DialogSet _dialogs;

        public EchoBot1Bot(EchoBot1Accessors accessors, ILoggerFactory loggerFactory)
        {
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
            _dialogs = new DialogSet(accessors.ConversationDialogState);

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                NameStepAsync,
                NameConfirmStepAsync,
                AgeStepAsync,
                ConfirmStepAsync,
                SummaryStepAsync,
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            _dialogs
                .Add(new TextPrompt("echo"))
                .Add(new TextPrompt("name"))
                .Add(new DateTimePrompt("birthdate"))
                .Add(new ConfirmPrompt("confirm"));

            //Root Dialog Flow
            _dialogs.Add(new WaterfallDialog("rootDialog")
                .AddStep(EchoStepAsync)
                );

            //Get User Details Dialog  Flow
            _dialogs.Add(new WaterfallDialog("getUserDetails")
                .AddStep(NameStepAsync)
                .AddStep(NameConfirmStepAsync)
                .AddStep(AgeStepAsync)
                .AddStep(ConfirmStepAsync)
                .AddStep(SummaryStepAsync)
                );
            

            _logger = loggerFactory.CreateLogger<EchoBot1Bot>();
            _logger.LogTrace("Turn start.");
        }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
                var results = await dialogContext.ContinueDialogAsync(cancellationToken);

                // If the DialogTurnStatus is Empty we should start a new dialog.
                if (results.Status == DialogTurnStatus.Empty)
                {
                    if (turnContext.Activity.Text.Equals("Hallo"))
                    {
                        await dialogContext.BeginDialogAsync("getUserDetails", null, cancellationToken);
                    }
                    else
                    {
                        await dialogContext.Context.SendActivityAsync(MessageFactory.Text($"You said {turnContext.Activity.Text}."), cancellationToken);
                    }
                }
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (turnContext.Activity.MembersAdded != null)
                {
                    await SendWelcomeMessageAsync(turnContext, cancellationToken);
                }
            }
            else
            {
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected");
            }
            // Save the dialog state into the conversation state.
            await _accessors.ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);

            // Save the user profile updates into the user state.
            await _accessors.UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var reply = turnContext.Activity.CreateReply();
                    reply.Text = WelcomeText;
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            }
        }

        private async Task<DialogTurnResult> EchoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync("echo", new PromptOptions { Prompt = MessageFactory.Text("You said") }, cancellationToken);
        }

        private static async Task<DialogTurnResult> NameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
            // Running a prompt here means the next WaterfallStep will be run when the users response is received.
            return await stepContext.PromptAsync("name", new PromptOptions { Prompt = MessageFactory.Text("Hi my name is Baby bot, What is your name?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> NameConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the current profile object from user state.
            var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);

            // Update the profile.
            userProfile.Name = (string)stepContext.Result;

            // We can send messages to the user at any point in the WaterfallStep.
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Thanks {stepContext.Result}."), cancellationToken);

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
            return await stepContext.PromptAsync("confirm", new PromptOptions { Prompt = MessageFactory.Text("Would you like to give your birthdate?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> AgeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                // User said "yes" so we will be prompting for the age.

                // Get the current profile object from user state.
                var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);

                // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is a Prompt Dialog.
                return await stepContext.PromptAsync("birthdate", new PromptOptions { Prompt = MessageFactory.Text("Please enter your birthdate.") }, cancellationToken);
            }
            else
            {
                // User said "no" so we will skip the next step. Give -1 as the age.
                return await stepContext.NextAsync(default(DateTime), cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the current profile object from user state.

            var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);

            // Update the profile.;
            //userProfile.Birthdate = Convert.ToDateTime((string)stepContext.Result);

            //userProfile.Birthdate = Convert.ToDateTime((string)stepContext.Result);

            // We can send messages to the user at any point in the WaterfallStep.
            var birthDate = (stepContext.Result as IList<DateTimeResolution>)?.FirstOrDefault();
            var DateTimeBirthdate = Convert.ToDateTime(birthDate.Value ?? birthDate.Timex);
            
            if (DateTimeBirthdate == default(DateTime))
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"No birthdate given."), cancellationToken);
            }
            else
            {
                userProfile.Birthdate = DateTimeBirthdate;
                // We can send messages to the user at any point in the WaterfallStep. userProfile.Birthdate.ToString("d", DateTimeFormatInfo.InvariantInfo)
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"I have your birthdate as {userProfile.Birthdate.ToString("yyyy/MM/dd")} ."), cancellationToken);
            }

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is a Prompt Dialog.
            return await stepContext.PromptAsync("confirm", new PromptOptions { Prompt = MessageFactory.Text("Is this ok?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                // Get the current profile object from user state.
                var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);

                // We can send messages to the user at any point in the WaterfallStep.
                if (userProfile.Birthdate.Equals(""))
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text($"I have your name as {userProfile.Name}."), cancellationToken);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text($"I have your name as {userProfile.Name} and birthdate as {userProfile.Birthdate}."), cancellationToken);
                }
            }
            else
            {
                // We can send messages to the user at any point in the WaterfallStep.
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thanks. Your profile will not be kept."), cancellationToken);
            }

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is the end.
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
