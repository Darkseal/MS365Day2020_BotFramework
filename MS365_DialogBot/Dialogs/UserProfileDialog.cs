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

            // Definisco l'array di WaterfallStep (da eseguire in sequenza)
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

            // Aggiungo le dialog che saranno presentate agli utenti
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>), AgePromptValidatorAsync));

            // Definisco la dialog iniziale (quella da cui far partire l'utente)
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> NameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Recupero il profilo utente dall'UserState tramite l'userProfileAccessor
            var userProfile = await _userProfileAccessor.GetAsync(
                stepContext.Context, () => new UserProfile(),
                cancellationToken);

            // Imposto la proprietà IsRegistering a TRUE
            userProfile.IsRegistering = true;

            // Ogni WaterfallStep deve terminare restituendo una dialog oppure la fine della waterfall:
            // in questo caso restituiamo una dialog di tipo "TextPrompt", che ha lo scopo di acquisire una risposta da parte dell'utente.
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

            // E' possibile inviare messaggi da qualsiasi WaterfallStep (ovvero in qualsiasi momento della waterfall)
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
                // Se l'utente ha dichiarato di voler comunicare l'età, presento una "NumberPrompt" dialog per acquisirla.
                // La NumberPrompt dialog è simile alla TextPrompt, ma accetta solo valori numerici:
                // se l'utente inserisce un valore non numerico, oppure il valore impostato non viene convalidato dal validator method
                // che abbiamo definito come parametro al momento di instanziare la NumberPrompt Dialog, 
                // è previsto un RetryPrompt da presentare a schermo.
                var promptOptions = new PromptOptions
                {
                    Prompt = MessageFactory.Text("Perfetto! Inserisci la tua età."),
                    RetryPrompt = MessageFactory.Text("ATTENZIONE: devi inserire un valore numerico intero compreso tra 0 e 150."),
                };

                return await stepContext.PromptAsync(nameof(NumberPrompt<int>), promptOptions, cancellationToken);
            }
            else
            {
                // Se l'utente ha dichiarato di non voler comunicare l'età, salto direttamente al WaterfallStep successivo.

                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text($"Nessun problema, {stepContext.Values["name"]}! Continuiamo pure."),
                    cancellationToken);

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

            // Recupero il profilo utente dall'UserState così da poter presentare il riepilogo dei dati inseriti.
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
            // Recupero il profilo utente dall'UserState
            var userProfile = await _userProfileAccessor.GetAsync(
                stepContext.Context, () => new UserProfile(),
                cancellationToken);

            var value = ((FoundChoice)stepContext.Result).Value; 
            if (value == "Si")
            {
                // Se l'utente ha confermato la registrazione:

                // TODO: registro l'utente nel Database (o qualcosa del genere)

                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("OPERAZIONE COMPLETATA!"),
                    cancellationToken);

                // TODO: invio all'utente una e-mail di conferma registrazione.

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
                // Se l'utente non ha confermato la registrazione:

                // Elimino i dati inseriti
                stepContext.Values.Clear();

                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("Ricevuto: le informazioni che hai inserito non saranno memorizzate. Digita REGISTRAMI se vuoi provare ancora."), 
                    cancellationToken);
            }

            // a prescindere dalla scelta dell'utente, imposto la proprietà IsRegistering a FALSE poiché la procedura di registrazione si è conclusa.
            userProfile.IsRegistering = false;

            // restituisco la fine della Waterfall
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private static Task<bool> AgePromptValidatorAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            // convalido il dato relativo all'età inserita dall'utente (numerico, maggiore di 0, minore di 150)
            return Task.FromResult(promptContext.Recognized.Succeeded && promptContext.Recognized.Value > 0 && promptContext.Recognized.Value < 150);
        }
    }
}
