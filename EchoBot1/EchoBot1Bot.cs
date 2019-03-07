﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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
using System.IO;

namespace EchoBot1
{

    public class EchoBot1Bot : IBot
    {
        private const string WelcomeText = "Welcome to EchoBot. This bot will introduce multiple turns using prompts.  Type anything to get started.";
        private readonly EchoBot1Accessors _accessors;
        private readonly ILogger _logger;
        private DialogSet _dialogs;
        private dynamic array;

        public EchoBot1Bot(EchoBot1Accessors accessors, ILoggerFactory loggerFactory)
        {
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
            _dialogs = new DialogSet(accessors.ConversationDialogState);

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                NameStepAsync,
                BirthdateStepAsync,
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
            _dialogs.Add(new WaterfallDialog("getDetailsDialog")
                .AddStep(NameStepAsync)
                .AddStep(BirthdateStepAsync)
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
            using (StreamReader read = new StreamReader("questions.json"))
            {
                string json = read.ReadToEnd();
                dynamic questionArray = JsonConvert.DeserializeObject(json);
                array = questionArray;
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
                        string id = array.flowId;
                        await dialogContext.BeginDialogAsync(id, null, cancellationToken);
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

        private async Task<DialogTurnResult> NameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //userProfile.Name = (string)stepContext.Result;
            string message = array.questions[0].Text;
            return await stepContext.PromptAsync("name", new PromptOptions { Prompt = MessageFactory.Text(message) }, cancellationToken);
            //return await stepContext.PromptAsync("name", new PromptOptions { Prompt = MessageFactory.Text("Hi my name is Baby bot, What is your name?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> BirthdateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            userProfile.Name = (string)stepContext.Result;
            string message = array.questions[1].Text;
            return await stepContext.PromptAsync("birthdate", new PromptOptions { Prompt = MessageFactory.Text(message) }, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            var resolution = (stepContext.Result as IList<DateTimeResolution>)?.FirstOrDefault();
            DateTime date = Convert.ToDateTime(resolution.Value ?? resolution.Timex);
            userProfile.Birthdate = date;
            string birthdateYear = userProfile.Birthdate.ToString("yyyy");
            DateTime today = DateTime.Now;
            int years = today.Year - Convert.ToInt32(birthdateYear);

            // We can send messages to the user at any point in the WaterfallStep.
            if (userProfile.Birthdate == null)
            {
               await stepContext.Context.SendActivityAsync(MessageFactory.Text($"I have your name as {userProfile.Name}."), cancellationToken);
            }
            else
            {
               await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Hi {userProfile.Name}, so you were born on {userProfile.Birthdate.ToString("dd/MM/yyyy")} and you are {years} years old."), cancellationToken);
            }
            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is the end.
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
