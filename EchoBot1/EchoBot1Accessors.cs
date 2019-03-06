// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;

namespace EchoBot1
{
    public class EchoBot1Accessors
    {
        public EchoBot1Accessors(ConversationState conversationState, UserState userState)
        {
            ConversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            UserState = userState ?? throw new ArgumentNullException(nameof(userState));
        }
        public IStatePropertyAccessor<DialogState> ConversationDialogState { get; set; }
        public IStatePropertyAccessor<UserProfile> UserProfile { get; set; }
        
        public ConversationState ConversationState { get; }
        public UserState UserState { get; }
    }
}
