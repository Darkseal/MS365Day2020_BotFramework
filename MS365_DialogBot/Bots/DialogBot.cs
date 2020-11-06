// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using MS365_DialogBot;

namespace MS365_PromptBot
{
    public class DialogBot<T> : ActivityHandler where T : Dialog
    {
        /// <summary>
        /// lo stato della conversation all'interno della quale un utente rivolge un messaggio al bot, a prescindere dall’user.
        /// </summary>
        protected readonly BotState ConversationState;

        /// <summary>
        /// lo stato dell'utente che rivolge un messaggio al bot, a prescindere dalla conversation.
        /// </summary>
        protected readonly BotState UserState;

        /// <summary>
        /// lo stato dell'utente che rivolge un messaggio al bot all’interno di una conversation ben precisa.
        /// </summary>
        protected readonly BotState PrivateConversationState;

        protected readonly Dialog Dialog;

        /// <summary>
        /// la parola che determina l'avvio della procedura di registrazione
        /// </summary>
        protected readonly string TriggerText = "REGISTRAMI";

        public DialogBot(
            ConversationState conversationState, 
            UserState userState, 
            PrivateConversationState privateConversationState, 
            T dialog)
        {
            ConversationState = conversationState;
            UserState = userState;
            PrivateConversationState = privateConversationState;

            Dialog = dialog;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Salva gli eventuali cambiamenti di stato avvenuti durante il turno
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
            await PrivateConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // recuperiamo il profilo dell'utente corrente dall'UserState
            var userStateAccessor = UserState.CreateProperty<UserProfile>(nameof(UserProfile));
            var userProfile = await userStateAccessor.GetAsync(turnContext, () => new UserProfile());


            // La dialog relativa alla procedura di registrazione viene eseguita se una delle seguenti condizioni è TRUE:
            // - L'utente sta già effettuando la registrazione (nuovo step)
            // - L'utente ha chiesto di iniziare una nuova registrazione digitando il TriggerText
            if (userProfile.IsRegistering || turnContext.Activity.Text.Contains(TriggerText))
            {
                await Dialog.RunAsync(
                    turnContext,

                    // OPZIONE #1: Imposta il DialogState all'interno del ConversationState:
                    // in questo modo avremo una dialog comune a tutti gli utenti.

                    //ConversationState.CreateProperty<DialogState>(nameof(DialogState)),



                    // OPZIONE #2: Imposta il DialogState all'interno dell'UserState:
                    // in questo modo avremo una dialog per ciascun utente, a prescindere dalla chat utilizzata.

                    UserState.CreateProperty<DialogState>(nameof(DialogState)),



                    // OPZIONE #3: Imposta il DialogState all'interno del PrivateConversationState:
                    // in questo modo avremo una dialog per ciascun utente e per ciascuna chat utilizzata.

                    //PrivateConversationState.CreateProperty<DialogState>(nameof(DialogState)),

                    cancellationToken);
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var welcomeText = $"Benvenuto, {member.Name}: digita {TriggerText} per registrarti.";
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}
