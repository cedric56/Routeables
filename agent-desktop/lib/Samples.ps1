# What the XAnimals samples show, shared by run.ps1 (desktop) and run-android.ps1: the pages of the menu, the first animal of
# each list, the dialogs of the Dialogs page with their answers and the text of the Result line. Dot-sourced.

$allPages = 'Cats', 'Dogs', 'Monkeys', 'Elephants', 'Bears', 'Pandas', 'History', 'Regions', 'Adopt a pet', 'Dialogs', 'Features', 'Members', 'About'

# Each dialog of DialogsViewModel: the answers tried, in order, and what the Result line then shows. Never "Close": it is also the
# name of the button that closes the window, and a dialog that did not open would leave only that one to find. The first answer of each
# dialog is chosen so that the Result line changes from one dialog to the next (otherwise the check could pass on the old text).
$dialogs = @(
  @{ Button = 'Ok';                        Answers = @('OK');                     Results = @{ OK = 'Message closed.' } }
  @{ Button = 'Error';                     Answers = @('OK');                     Results = @{ OK = 'Error closed.' } }
  @{ Button = 'Yes / No';                  Answers = 'Yes', 'No';                 Results = @{ Yes = 'Answer: Yes.'; No = 'Answer: No.' } }
  @{ Button = 'Ok / Cancel';               Answers = 'Cancel', 'OK';              Results = @{ Cancel = 'Answer: Cancel.'; OK = 'Answer: OK.' } }
  @{ Button = 'Ok / Abort';                Answers = 'OK', 'Abort';               Results = @{ OK = 'Answer: OK.'; Abort = 'Answer: Abort.' } }
  @{ Button = 'Yes / No / Cancel';         Answers = 'No', 'Yes', 'Cancel';       Results = @{ No = 'Answer: No.'; Yes = 'Answer: Yes.'; Cancel = 'Answer: Cancel.' } }
  @{ Button = 'Yes / No / Abort';          Answers = 'Abort', 'Yes', 'No';        Results = @{ Abort = 'Answer: Abort.'; Yes = 'Answer: Yes.'; No = 'Answer: No.' } }
  @{ Button = 'Content dialog';            Answers = 'Send', 'Copy link', 'Cancel'; Results = @{ Send = 'Chose Send.'; 'Copy link' = 'Chose Copy link.'; Cancel = 'Closed without a choice.' } }
  @{ Button = 'Task dialog';               Answers = 'Yes', 'No', 'Cancel';       Results = @{ Yes = 'Answer: Yes.'; No = 'Answer: No.'; Cancel = 'Answer: Cancel.' } }
  @{ Button = 'Task dialog with progress'; Answers = 'Cancel';                    Results = @{ Cancel = 'Download cancelled.' } }
  @{ Button = 'Show modal';                Answers = @('Cancel');                 Results = @{ Cancel = 'Note cancelled.' } }
)
# The view model of each page: WPF and Avalonia name their menu entries after it ("...RouteableMenuEntry`1[...CatsViewModel]"),
# which tells whether a drawer is open.
$viewModels = @{
  'Cats' = 'CatsViewModel'; 'Dogs' = 'DogsViewModel'; 'Monkeys' = 'MonkeysViewModel'; 'Elephants' = 'ElephantsViewModel'
  'Bears' = 'BearsViewModel'; 'Pandas' = 'PandasViewModel'; 'History' = 'HistoryViewModel'; 'Regions' = 'RegionsViewModel'
  'Adopt a pet' = 'AdoptViewModel'; 'Dialogs' = 'DialogsViewModel'; 'Features' = 'FeaturesViewModel'
  'Members' = 'MembersViewModel'; 'About' = 'AboutViewModel'
}
# The order of the entries of MainViewModel's menu (Birds comes from a module; WinUI 3 adds Tabs at the end). MAUI's flyout shows
# no text in its entries: they are taken by rank.
$menuOrder = 'Cats', 'Dogs', 'Monkeys', 'Elephants', 'Bears', 'Pandas', 'History', 'Regions', 'Adopt a pet', 'Dialogs', 'Birds', 'Features', 'Members', 'About'
# The title the header shows for each page (Members: the guard sends a visitor who is signed out to Features).
$titles = @{}
foreach ($page in $allPages) { $titles[$page] = @($page) }
$titles['Members'] = @('Members', 'Features')
# The first animal of each list (samples/Bases/XAnimals.ViewModels/Data): opened, then Back.
$firstAnimal = @{
  'Cats' = 'Abyssinian'; 'Dogs' = 'Afghan Hound'; 'Monkeys' = 'Baboon'; 'Elephants' = 'African Bush Elephant'
  'Bears' = 'American Black Bear'; 'Pandas' = 'Giant Panda'
}
$errorWords = 'Exception', 'Unhandled', 'Error:', 'Erreur', 'could not be located'
