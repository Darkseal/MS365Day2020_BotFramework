using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using MS365_PromptBot;

namespace MS365_DialogBot.Dialogs
{
    public class UserProfileDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<UserProfile> _userProfileAccessor;

        public UserProfileDialog(UserState userState)
            : base(nameof(UserProfileDialog))
        {
            _userProfileAccessor = userState.CreateProperty<UserProfile>(nameof(UserProfile));

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                NameStepAsync,
                NameConfirmStepAsync,
                AgeStepAsync,
                CityStepAsync,
                FavouriteLanguageStepAsync,
                ConfirmStepAsync,
                SummaryStepAsync,
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>), AgePromptValidatorAsync));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> NameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the current profile object from user state.
            var userProfile = await _userProfileAccessor.GetAsync(
                stepContext.Context, () => new UserProfile(),
                cancellationToken);

            // set IsRegistering to TRUE, since the user initiated the registration process.
            userProfile.IsRegistering = true;

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
            // Running a prompt here means the next WaterfallStep will be run when the user's response is received.
            return await stepContext.PromptAsync(
                nameof(TextPrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("D'accordo! Per prima cosa, inserisci il tuo nome.")
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> NameConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["name"] = (string)stepContext.Result;

            // We can send messages to the user at any point in the WaterfallStep.
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Grazie mille, {stepContext.Result}."), cancellationToken);

            return await stepContext.PromptAsync(
                nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Desideri comunicare la tua età?"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Si", "No" })
                },
                cancellationToken);
        }

        private async Task<DialogTurnResult> AgeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var value = ((FoundChoice)stepContext.Result).Value;
            if (value == "Si")
            {
                // User said "yes" so we will be prompting for the age.
                // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
                var promptOptions = new PromptOptions
                {
                    Prompt = MessageFactory.Text("Perfetto! Inserisci la tua età."),
                    RetryPrompt = MessageFactory.Text("ATTENZIONE: devi inserire un valore numerico intero compreso tra 0 e 150."),
                };

                return await stepContext.PromptAsync(nameof(NumberPrompt<int>), promptOptions, cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text($"Nessun problema, {stepContext.Values["name"]}! Continuiamo pure."),
                    cancellationToken);

                // User said "no" so we will skip the next step. Give -1 as the age.
                return await stepContext.NextAsync(-1, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> CityStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["age"] = (int)stepContext.Result;

            return await stepContext.PromptAsync(
                nameof(TextPrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Inserisci la tua città di provenienza.")
                }, cancellationToken);
        }

        private static async Task<DialogTurnResult> FavouriteLanguageStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["city"] = (string)stepContext.Result;

            return await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Inserisci il tuo linguaggio di programmazione preferito:"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "C#", "Java", "JavaScript", "TypeScript", "Java", "Python", "Go", "R", "Altro" }),
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["language"] = ((FoundChoice)stepContext.Result).Value;

            await stepContext.Context.SendActivityAsync(
                MessageFactory.Text($"Grazie per le risposte, {stepContext.Values["name"]}. Ecco un riepilogo dei dati che hai inserito:"),
                cancellationToken);

            // Get the current profile object from user state.
            var userProfile = await _userProfileAccessor.GetAsync(
                stepContext.Context, () => new UserProfile(),
                cancellationToken);

            userProfile.Name = (string)stepContext.Values["name"];
            userProfile.Age = (int)stepContext.Values["age"];
            userProfile.City = (string)stepContext.Values["city"];
            userProfile.Language = (string)stepContext.Values["language"];

            var msg = $"Il tuo nome è {userProfile.Name}";

            if (userProfile.Age != -1)
                msg += $", hai {userProfile.Age} anni";

            msg += $", vieni da {userProfile.City} e il tuo linguaggio preferito è { userProfile.Language}.";

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);

            return await stepContext.PromptAsync(
                nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Confermi le tue scelte?"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Si", "No" })
                },
                cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the current profile object from user state.
            var userProfile = await _userProfileAccessor.GetAsync(
                stepContext.Context, () => new UserProfile(),
                cancellationToken);

            var value = ((FoundChoice)stepContext.Result).Value; 
            if (value == "Si")
            {
                // TODO: register the user in a Database (or something like that)

                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("OPERAZIONE COMPLETATA!"),
                    cancellationToken);

                // TODO: send the user a confirmation e-mail about his/her successful registration.

                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text($"Grazie per esserti registrato, {userProfile.Name}: " +
                        "riceverai a breve una e-mail di conferma contenente il riepilogo dei dati inseriti."),
                    cancellationToken);

                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("Non vediamo l'ora di averti con noi: a presto!"),
                    cancellationToken);
            }
            else
            {
                // removes the collected user info
                stepContext.Values.Clear();

                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("Ricevuto: le informazioni che hai inserito non saranno memorizzate. Digita REGISTRAMI se vuoi provare ancora."), 
                    cancellationToken);
            }

            // regardless of how it went, set IsRegistering to FALSE since the registration process is over.
            userProfile.IsRegistering = false;

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is the end.
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private static Task<bool> AgePromptValidatorAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            // This condition is our validation rule. You can also change the value at this point.
            return Task.FromResult(promptContext.Recognized.Succeeded && promptContext.Recognized.Value > 0 && promptContext.Recognized.Value < 150);
        }
    }
}
