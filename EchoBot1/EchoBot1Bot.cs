// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
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
                AgeStepAsync,
                SummaryStepAsync,
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            _dialogs
                .Add(new TextPrompt("echo"))
                .Add(new TextPrompt("name"))
                .Add(new DateTimePrompt("age"))
                .Add(new ConfirmPrompt("confirm"));

            //Root Dialog Flow
            _dialogs.Add(new WaterfallDialog("rootDialog")
                .AddStep(EchoStepAsync)
                );

            //Get User Details Dialog  Flow
            _dialogs.Add(new WaterfallDialog("getDetailsDialog")
                .AddStep(NameStepAsync)
                .AddStep(AgeStepAsync)
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
                        await dialogContext.BeginDialogAsync("getDetailsDialog", null, cancellationToken);
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
            //userProfile.Name = (string)stepContext.Result;
            return await stepContext.PromptAsync("name", new PromptOptions { Prompt = MessageFactory.Text("Hi my name is Baby bot, What is your name?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> AgeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the current profile object from user state.
            var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            userProfile.Name = (string)stepContext.Result;
            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is a Prompt Dialog.
            return await stepContext.PromptAsync("age", new PromptOptions { Prompt = MessageFactory.Text("Please enter your birthdate.") }, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            var resolution = (stepContext.Result as IList<DateTimeResolution>)?.FirstOrDefault();
            DateTime date = Convert.ToDateTime(resolution.Value ?? resolution.Timex);
            //date = date.Date;
            userProfile.Birthdate = date;
            //userProfile.Birthdate = userProfile.Birthdate.Date;

            // We can send messages to the user at any point in the WaterfallStep.
            if (userProfile.Birthdate == null)
            {
               await stepContext.Context.SendActivityAsync(MessageFactory.Text($"I have your name as {userProfile.Name}."), cancellationToken);
            }
            else
            {
               await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Hi {userProfile.Name}, so you were born on {userProfile.Birthdate.ToString("dd/MM/yyyy")}."), cancellationToken);
            }

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is the end.
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
