# Third-party notices

Routeables is licensed under the MIT License (see `LICENSE.txt`). The samples contain material from other sources, under their own terms.

## Xaminals (Xamarin Inc, Microsoft)

The animal pages of the samples (`Samples/`, namespaces `Xaminals.Data` and `XAminals.Models`, the list of animals and their details) are adapted from the
Xaminals sample for Xamarin.Forms Shell.

```
Copyright 2011 Xamarin Inc

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
```

Changes: the sample was rewritten for Routeables (view-model-first routing, a view model per page, shared `XAnimals.Common` and `XAnimals.ViewModels`
libraries), ported to Blazor (Razor), Avalonia, .NET MAUI and Uno Platform, split into modules (`XAnimals.Birds` and one project per platform), and extended
with new animals and pages. The full text of the Apache License 2.0 is at the end of this file.

## Images

The samples show photographs that are loaded from Wikimedia Commons when the application runs; none is stored in this repository. Each file has its own author
and license (often a Creative Commons license that asks for the name of the author), shown on its Commons page. The author and license columns are read from
those pages by `tools/ImageCredits` (`dotnet run --project tools/ImageCredits` from the root of the repository; it needs the network). Run it again when a photograph is
added. A row that says "to fill" has not been read yet.

| Subject | File on Wikimedia Commons | Author | License |
|---|---|---|---|
| Abyssinian | [Gustav_chocolate.jpg](https://commons.wikimedia.org/wiki/File:Gustav_chocolate.jpg) | Karin Langner-Bahmann | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| Afghan Hound | [Afghane.jpg](https://commons.wikimedia.org/wiki/File:Afghane.jpg) | unknown (see the Commons page) | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| African Bush Elephant | [African_Elephant_(Loxodonta_africana)_bull_(31100819046).jpg](https://commons.wikimedia.org/wiki/File:African_Elephant_(Loxodonta_africana)_bull_(31100819046).jpg) | Bernard DUPONT from FRANCE | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Alpine Dachsbracke | [Alpejski_gończy_krótkonożny_g99.jpg](https://commons.wikimedia.org/wiki/File:Alpejski_go%C5%84czy_kr%C3%B3tkono%C5%BCny_g99.jpg) | Pleple2000 | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| American Black Bear | [01_Schwarzbär.jpg](https://commons.wikimedia.org/wiki/File:01_Schwarzb%C3%A4r.jpg) | Diginatur | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| American Bulldog | [American_Bulldog_600.jpg](https://commons.wikimedia.org/wiki/File:American_Bulldog_600.jpg) | Photo by sannse | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| Arabian Mau | [Bex_Arabian_Mau.jpg](https://commons.wikimedia.org/wiki/File:Bex_Arabian_Mau.jpg) | SolnishkoA | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Asian Black Bear | [Ursus_thibetanus_3_(Wroclaw_zoo).JPG](https://commons.wikimedia.org/wiki/File:Ursus_thibetanus_3_(Wroclaw_zoo).JPG) | Guérin Nicolas ( messages ) | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Baboon | [Papio_anubis_(Serengeti,_2009).jpg](https://commons.wikimedia.org/wiki/File:Papio_anubis_(Serengeti,_2009).jpg) | Yathin S Krishnappa | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Bay-headed Tanager | [Bay-headed_Tanager.jpg](https://commons.wikimedia.org/wiki/File:Bay-headed_Tanager.jpg) | Dominic Sherony | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Bearded Collie | [Bearded_Collie_600.jpg](https://commons.wikimedia.org/wiki/File:Bearded_Collie_600.jpg) | unknown (see the Commons page) | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| Bengal | [Paintedcats_Red_Star_standing.jpg](https://commons.wikimedia.org/wiki/File:Paintedcats_Red_Star_standing.jpg) | User:Lightburst | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0) |
| Black Snub-nosed Monkey | [RhinopitecusBieti.jpg](https://commons.wikimedia.org/wiki/File:RhinopitecusBieti.jpg) | Dreline | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Blue Monkey | [BlueMonkey.jpg](https://commons.wikimedia.org/wiki/File:BlueMonkey.jpg) | Pedro Gonnet | [CC BY 2.5](https://creativecommons.org/licenses/by/2.5) |
| Blue-necked Tanager | [Blue-necked_Tanager_(Tangara_cyanicollis)_(4856996470).jpg](https://commons.wikimedia.org/wiki/File:Blue-necked_Tanager_(Tangara_cyanicollis)_(4856996470).jpg) | Dominic Sherony | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Borneo Elephant | [Elephant_@_kabini.jpg](https://commons.wikimedia.org/wiki/File:Elephant_%40_kabini.jpg) | Manojiritty | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0) |
| Boston Terrier | [Boston-terrier-carlos-de.JPG](https://commons.wikimedia.org/wiki/File:Boston-terrier-carlos-de.JPG) | Andreas Schlaugat | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0) |
| Brown Bear | [Kamchatka_Brown_Bear_near_Dvuhyurtochnoe_on_2015-07-23.jpg](https://commons.wikimedia.org/wiki/File:Kamchatka_Brown_Bear_near_Dvuhyurtochnoe_on_2015-07-23.jpg) | Robert F. Tobler | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0) |
| Burmese | [Blissandlucky11.jpg](https://commons.wikimedia.org/wiki/File:Blissandlucky11.jpg) | Bjtripp2 at English Wikipedia | Public domain |
| Burnished-buff Tanager | [Tangara_cayana_Tangara_Triguera_Burnished-buff_Tanager_(15792282823)_(cropped).jpg](https://commons.wikimedia.org/wiki/File:Tangara_cayana_Tangara_Triguera_Burnished-buff_Tanager_(15792282823)_(cropped).jpg) | Félix Uribe from Rionegro, Antioquia, Colombia | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| California Grizzly Bear | [Monarch_the_bear.jpg](https://commons.wikimedia.org/wiki/File:Monarch_the_bear.jpg) | Payton from chicago, usa | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0) |
| Canadian Eskimo | [Spoonsced.jpg](https://commons.wikimedia.org/wiki/File:Spoonsced.jpg) | Exhaustfumes at English Wikipedia | Public domain |
| Cave Bear | [Teufelshöhle-Höhlenbär-Dreiviertelprofil.jpg](https://commons.wikimedia.org/wiki/File:Teufelsh%C3%B6hle-H%C3%B6hlenb%C3%A4r-Dreiviertelprofil.jpg) | Ra'ike (see also: de:Benutzer:Ra'ike ) | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Cyprus | [CyprusShorthair.jpg](https://commons.wikimedia.org/wiki/File:CyprusShorthair.jpg) | Riannacone | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Desert Elephant | [Desert_elephants_in_the_Huab_River.jpg](https://commons.wikimedia.org/wiki/File:Desert_elephants_in_the_Huab_River.jpg) | Greg Willis from Arlington, VA, usa | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Dwarf Elephant | [Elephas_skeleton.JPG](https://commons.wikimedia.org/wiki/File:Elephas_skeleton.JPG) | Ninjatacoshell | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Eurohound | [Eurohound.jpg](https://commons.wikimedia.org/wiki/File:Eurohound.jpg) | Davemeisenheimer at English Wikipedia | Public domain |
| Gelada | [Gelada-Pavian.jpg](https://commons.wikimedia.org/wiki/File:Gelada-Pavian.jpg) | Kolumbusjogger | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| German Rex | [German_rex_harry_(cropped).jpg](https://commons.wikimedia.org/wiki/File:German_rex_harry_(cropped).jpg) | M.Gerver | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| Giant Panda | [Grosser_Panda.JPG](https://commons.wikimedia.org/wiki/File:Grosser_Panda.JPG) | J. Patrick Fischer | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Giant Panda | [Lightmatter_panda.jpg](https://commons.wikimedia.org/wiki/File:Lightmatter_panda.jpg) | Aaron Logan | [CC BY 1.0](https://creativecommons.org/licenses/by/1.0) |
| Golden Lion Tamarin | [Golden_lion_tamarin_portrait3.jpg](https://commons.wikimedia.org/wiki/File:Golden_lion_tamarin_portrait3.jpg) | Jeroen Kransen | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Golden Snub-nosed Monkey | [Golden_Snub-nosed_Monkeys,_Qinling_Mountains_-_China.jpg](https://commons.wikimedia.org/wiki/File:Golden_Snub-nosed_Monkeys,_Qinling_Mountains_-_China.jpg) | Giovanni Mari | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Golden Tanager | [Tangara_arthus_-_Tángara_dorada_-_Golden_Tanager_(8542820434).jpg](https://commons.wikimedia.org/wiki/File:Tangara_arthus_-_T%C3%A1ngara_dorada_-_Golden_Tanager_(8542820434).jpg) | Félix Uribe from Colombia | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0) |
| Gray-shanked Douc | [Cuc.Phuong.Primate.Rehab.center.jpg](https://commons.wikimedia.org/wiki/File:Cuc.Phuong.Primate.Rehab.center.jpg) | http://wikitravel.org/en/Special:Contributions/David_Straub | Public domain |
| Green-headed Tanager | [Green-headed_Tanager_(Tangara_seledon)_(8077715480).jpg](https://commons.wikimedia.org/wiki/File:Green-headed_Tanager_(Tangara_seledon)_(8077715480).jpg) | Ron Knight from Seaford, East Sussex, United Kingdom | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0) |
| Grizzly-Polar Bear Hybrid | [Grolar.JPG](https://commons.wikimedia.org/wiki/File:Grolar.JPG) | Corradox | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Highlander | [Highlander-7.jpg](https://commons.wikimedia.org/wiki/File:Highlander-7.jpg) | DigitalDirt | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0) |
| Howler Monkey | [Alouatta_guariba.jpg](https://commons.wikimedia.org/wiki/File:Alouatta_guariba.jpg) | Paulo B. Chaves | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0) |
| Indian Elephant | [Elephas_maximus_(Bandipur).jpg](https://commons.wikimedia.org/wiki/File:Elephas_maximus_(Bandipur).jpg) | Yathin S Krishnappa | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Irish Terrier | [IrishTerrierSydenhamHillWoods.jpg](https://commons.wikimedia.org/wiki/File:IrishTerrierSydenhamHillWoods.jpg) | Andrew Clark | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0) |
| Japanese Macaque | [Macaca_fuscata_fuscata1.jpg](https://commons.wikimedia.org/wiki/File:Macaca_fuscata_fuscata1.jpg) | KENPEI | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Kerry Beagle | [Kerry_Beagle_from_1915.JPG](https://commons.wikimedia.org/wiki/File:Kerry_Beagle_from_1915.JPG) | not specified (except those with signature on image) | Public domain |
| Mammoth | [Columbian_mammoth.JPG](https://commons.wikimedia.org/wiki/File:Columbian_mammoth.JPG) | WolfmanSF | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Mandrill | [Mandrill_at_san_francisco_zoo.jpg](https://commons.wikimedia.org/wiki/File:Mandrill_at_san_francisco_zoo.jpg) | ((brian)) from Sebastopol, CA, USA | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0) |
| Mastodon | [Mammut_americanum.jpg](https://commons.wikimedia.org/wiki/File:Mammut_americanum.jpg) | Ryan Somma | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Norwegian Buhund | [Norwegian_Buhund_600.jpg](https://commons.wikimedia.org/wiki/File:Norwegian_Buhund_600.jpg) | unknown (see the Commons page) | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| Paradise Tanager | [Paradise_Tanager_Woodland_Parks_Zoo_RWD.jpg](https://commons.wikimedia.org/wiki/File:Paradise_Tanager_Woodland_Parks_Zoo_RWD.jpg) | DickDaniels ( http://carolinabirds.org/ ) | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0) |
| Patterdale Terrier | [05078045_Patterdale_Terrier.jpg](https://commons.wikimedia.org/wiki/File:05078045_Patterdale_Terrier.jpg) | Alephalpha | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Peterbald | [Peterbald_male_Shango_by_Irina_Polunina.jpg](https://commons.wikimedia.org/wiki/File:Peterbald_male_Shango_by_Irina_Polunina.jpg) | Peterbald | Public domain |
| Polar Bear | [Polar_Bear_-_Alaska_(cropped).jpg](https://commons.wikimedia.org/wiki/File:Polar_Bear_-_Alaska_(cropped).jpg) | Alan Wilson | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Proboscis Monkey | [Proboscis_Monkey_in_Borneo.jpg](https://commons.wikimedia.org/wiki/File:Proboscis_Monkey_in_Borneo.jpg) | David Dennis | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Purple-faced Langur | [Semnopithèque_blanchâtre_mâle.JPG](https://commons.wikimedia.org/wiki/File:Semnopith%C3%A8que_blanch%C3%A2tre_m%C3%A2le.JPG) | BluesyPete | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Pygmy Elephant | [Borneo-elephant-PLoS_Biology.jpg](https://commons.wikimedia.org/wiki/File:Borneo-elephant-PLoS_Biology.jpg) | PLoS Biology | [CC BY 2.5](https://creativecommons.org/licenses/by/2.5) |
| Pygmy Mammoth | [Mammuthus_exilis.jpg](https://commons.wikimedia.org/wiki/File:Mammuthus_exilis.jpg) | Franko Fonseca from Redondo Beach, USA | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Red Panda | [Red_Panda_(24986761703).jpg](https://commons.wikimedia.org/wiki/File:Red_Panda_(24986761703).jpg) | Mathias Appel | [CC0](http://creativecommons.org/publicdomain/zero/1.0/deed.en) |
| Red-shanked Douc | [Portrait_of_a_Douc.jpg](https://commons.wikimedia.org/wiki/File:Portrait_of_a_Douc.jpg) | Bjørn Christian Tørrissen | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Saffron-crowned Tanager | [Saffron-crowned_Tanager_(Tangara_xanthocephala)_(cropped).jpg](https://commons.wikimedia.org/wiki/File:Saffron-crowned_Tanager_(Tangara_xanthocephala)_(cropped).jpg) | https://www.flickr.com/photos/birding-colombia/ | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0) |
| Scottish Fold | [Adult_Scottish_Fold.jpg](https://commons.wikimedia.org/wiki/File:Adult_Scottish_Fold.jpg) | Mihai Matei | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Short-faced Bear | [ArctodusSimusSkeleton.jpg](https://commons.wikimedia.org/wiki/File:ArctodusSimusSkeleton.jpg) | Riku64 | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Sloth Bear | [Sloth_Bear_Washington_DC.JPG](https://commons.wikimedia.org/wiki/File:Sloth_Bear_Washington_DC.JPG) | Asiir | Public domain |
| Spectacled Bear | [Spectacled_Bear_-_Houston_Zoo.jpg](https://commons.wikimedia.org/wiki/File:Spectacled_Bear_-_Houston_Zoo.jpg) | en:User:Cburnett | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| Sphynx | [Sphinx2_July_2006.jpg](https://commons.wikimedia.org/wiki/File:Sphinx2_July_2006.jpg) | M.Minderhoud at Dutch Wikipedia | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| Squirrel Monkey | [Saimiri_sciureus-1_Luc_Viatour.jpg](https://commons.wikimedia.org/wiki/File:Saimiri_sciureus-1_Luc_Viatour.jpg) | Luc Viatour | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| Sri Lankan Elephant | [Srilankan_tuskelephant.jpg](https://commons.wikimedia.org/wiki/File:Srilankan_tuskelephant.jpg) | w:en:Faslan ( talk / contribs ) | [CC BY-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) |
| St. Bernard | [Hummel_Vedor_vd_Robandahoeve.jpg](https://commons.wikimedia.org/wiki/File:Hummel_Vedor_vd_Robandahoeve.jpg) | Blanky | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Sumatran Elephant | [Borobudur-Temple-Park_Elephant-cage-01.jpg](https://commons.wikimedia.org/wiki/File:Borobudur-Temple-Park_Elephant-cage-01.jpg) | CEphoto, Uwe Aranas | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0) |
| Sun Bear | [Sitting_sun_bear.jpg](https://commons.wikimedia.org/wiki/File:Sitting_sun_bear.jpg) | Tambako The Jaguar | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |
| Thomas's Langur | [Thomas's_langur_Presbytis_thomasi.jpg](https://commons.wikimedia.org/wiki/File:Thomas's_langur_Presbytis_thomasi.jpg) | Madeleine Holland | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0) |
| Tonkin Snub-nosed Monkey | [Tonkin_snub-nosed_monkeys_(Rhinopithecus_avunculus).jpg](https://commons.wikimedia.org/wiki/File:Tonkin_snub-nosed_monkeys_(Rhinopithecus_avunculus).jpg) | Quyet Le | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0) |

## Fonts

The samples ship these fonts (the files are in the repository). Each keeps its own license; the copyright lines are those written in the font files.

| Font | Files | Copyright | License |
|---|---|---|---|
| Inter | `Samples/XAnimals.Maui/Resources/Fonts/Inter-*.otf`, `Samples/XAnimals.Razor/wwwroot/fonts/Inter-*.otf` | Copyright (c) 2016 The Inter Project Authors (https://github.com/rsms/inter) | SIL Open Font License 1.1, [text](licenses/Inter-OFL.txt) |
| Lora | `Lora-Regular.ttf` (version 3.021, the file of the foundry, unmodified) in `Samples/XAnimals.Avalonia/Assets/Fonts`, `Samples/XAnimals.Maui/Resources/Fonts` and `Samples/XAnimals.Razor/wwwroot/fonts` | Copyright 2011 The Lora Project Authors (https://github.com/cyrealtype/Lora-Cyrillic), with Reserved Font Name "Lora" | SIL Open Font License 1.1, [text](licenses/Lora-OFL.txt) |
| Noto Emoji | `NotoEmoji.ttf` (the variable font of Google Fonts, fixed at weight 400 with fontTools, otherwise unmodified) in `Samples/XAnimals.Avalonia.Browser/Assets/Fonts`: the fallback of the emoji of the menu in the browser, which has no system font | Copyright 2013 Google LLC | SIL Open Font License 1.1, [text](licenses/NotoEmoji-OFL.txt) |
| Open Sans | `OpenSans-Regular.ttf` in `Samples/XAnimals.Maui/Resources/Fonts` and `Samples/XAnimals.MauiHybrid/Resources/Fonts` (from the .NET MAUI project template) | Digitized data copyright (c) 2010-2011, Google Corporation | Apache License 2.0, as written in the font file (text at the end of this file) |

The Open Font License lets the fonts be used, embedded and redistributed with software, and asks that the copyright notice and the license go with them, and that the fonts
are not sold on their own.

## Other files of the samples

| File | Origin | License |
|---|---|---|
| `Samples/XAnimals.Maui/Resources/Images/icon_*.png` | Emoji pictures that look like [Google Noto Emoji](https://github.com/googlefonts/noto-emoji) (**to confirm: where they were taken from**) | Noto Emoji is published under the SIL Open Font License 1.1 by Google |
| `Samples/XAnimals.MauiHybrid/Resources/Images/dotnet_bot.svg` | The .NET bot of the .NET MAUI project template | The illustrations of [dotnet/brand](https://github.com/dotnet/brand) are CC0 1.0 (no credit required) |

## Descriptions

The descriptions of the animals (the `Details` text in `Samples/**/Data` and `Samples/Modules/XAnimals.Birds`) are taken from the English Wikipedia articles listed
below, and may be shortened or adapted. Wikipedia text is licensed under the
[Creative Commons Attribution-ShareAlike 4.0 International License](https://creativecommons.org/licenses/by-sa/4.0/) (CC BY-SA 4.0). It is credited to the
contributors of each article, whose names are on the history page of the article. **These texts are not under the MIT License of Routeables**: if you reuse them, you
must keep this credit and share your adaptations under the same license.

<!-- articles:begin -->
| Subject | Article | Authors |
|---|---|---|
| Abyssinian | to fill ("Abyssinian" is a [disambiguation page](https://en.wikipedia.org/wiki/Abyssinian)) | to fill |
| Afghan Hound | [Afghan Hound](https://en.wikipedia.org/wiki/Afghan_Hound) | [history](https://en.wikipedia.org/w/index.php?title=Afghan_Hound&action=history) |
| African Bush Elephant | [African bush elephant](https://en.wikipedia.org/wiki/African_bush_elephant) | [history](https://en.wikipedia.org/w/index.php?title=African_bush_elephant&action=history) |
| Alpine Dachsbracke | [Alpine Dachsbracke](https://en.wikipedia.org/wiki/Alpine_Dachsbracke) | [history](https://en.wikipedia.org/w/index.php?title=Alpine_Dachsbracke&action=history) |
| American Black Bear | [American black bear](https://en.wikipedia.org/wiki/American_black_bear) | [history](https://en.wikipedia.org/w/index.php?title=American_black_bear&action=history) |
| American Bulldog | [American Bulldog](https://en.wikipedia.org/wiki/American_Bulldog) | [history](https://en.wikipedia.org/w/index.php?title=American_Bulldog&action=history) |
| Arabian Mau | [Arabian Mau](https://en.wikipedia.org/wiki/Arabian_Mau) | [history](https://en.wikipedia.org/w/index.php?title=Arabian_Mau&action=history) |
| Asian Black Bear | [Asian black bear](https://en.wikipedia.org/wiki/Asian_black_bear) | [history](https://en.wikipedia.org/w/index.php?title=Asian_black_bear&action=history) |
| Baboon | [Baboon](https://en.wikipedia.org/wiki/Baboon) | [history](https://en.wikipedia.org/w/index.php?title=Baboon&action=history) |
| Bay-headed Tanager | [Bay-headed tanager](https://en.wikipedia.org/wiki/Bay-headed_tanager) | [history](https://en.wikipedia.org/w/index.php?title=Bay-headed_tanager&action=history) |
| Bearded Collie | [Bearded Collie](https://en.wikipedia.org/wiki/Bearded_Collie) | [history](https://en.wikipedia.org/w/index.php?title=Bearded_Collie&action=history) |
| Bengal | [Bengal](https://en.wikipedia.org/wiki/Bengal) | [history](https://en.wikipedia.org/w/index.php?title=Bengal&action=history) |
| Black Snub-nosed Monkey | [Black-and-white snub-nosed monkey](https://en.wikipedia.org/wiki/Black-and-white_snub-nosed_monkey) | [history](https://en.wikipedia.org/w/index.php?title=Black-and-white_snub-nosed_monkey&action=history) |
| Blue Monkey | [Blue monkey](https://en.wikipedia.org/wiki/Blue_monkey) | [history](https://en.wikipedia.org/w/index.php?title=Blue_monkey&action=history) |
| Blue-necked Tanager | [Blue-necked tanager](https://en.wikipedia.org/wiki/Blue-necked_tanager) | [history](https://en.wikipedia.org/w/index.php?title=Blue-necked_tanager&action=history) |
| Borneo Elephant | [Borneo elephant](https://en.wikipedia.org/wiki/Borneo_elephant) | [history](https://en.wikipedia.org/w/index.php?title=Borneo_elephant&action=history) |
| Boston Terrier | [Boston Terrier](https://en.wikipedia.org/wiki/Boston_Terrier) | [history](https://en.wikipedia.org/w/index.php?title=Boston_Terrier&action=history) |
| Brown Bear | [Brown bear](https://en.wikipedia.org/wiki/Brown_bear) | [history](https://en.wikipedia.org/w/index.php?title=Brown_bear&action=history) |
| Burmese | to fill ("Burmese" is a [disambiguation page](https://en.wikipedia.org/wiki/Burmese)) | to fill |
| Burnished-buff Tanager | [Burnished-buff tanager](https://en.wikipedia.org/wiki/Burnished-buff_tanager) | [history](https://en.wikipedia.org/w/index.php?title=Burnished-buff_tanager&action=history) |
| California Grizzly Bear | [California grizzly bear](https://en.wikipedia.org/wiki/California_grizzly_bear) | [history](https://en.wikipedia.org/w/index.php?title=California_grizzly_bear&action=history) |
| Canadian Eskimo | to fill | to fill |
| Cave Bear | [Cave bear](https://en.wikipedia.org/wiki/Cave_bear) | [history](https://en.wikipedia.org/w/index.php?title=Cave_bear&action=history) |
| Cyprus | [Cyprus](https://en.wikipedia.org/wiki/Cyprus) | [history](https://en.wikipedia.org/w/index.php?title=Cyprus&action=history) |
| Desert Elephant | [Desert elephant](https://en.wikipedia.org/wiki/Desert_elephant) | [history](https://en.wikipedia.org/w/index.php?title=Desert_elephant&action=history) |
| Dwarf Elephant | to fill | to fill |
| Eurohound | [Eurohound](https://en.wikipedia.org/wiki/Eurohound) | [history](https://en.wikipedia.org/w/index.php?title=Eurohound&action=history) |
| Gelada | [Gelada](https://en.wikipedia.org/wiki/Gelada) | [history](https://en.wikipedia.org/w/index.php?title=Gelada&action=history) |
| German Rex | [German Rex](https://en.wikipedia.org/wiki/German_Rex) | [history](https://en.wikipedia.org/w/index.php?title=German_Rex&action=history) |
| Giant Panda | [Giant panda](https://en.wikipedia.org/wiki/Giant_panda) | [history](https://en.wikipedia.org/w/index.php?title=Giant_panda&action=history) |
| Golden Lion Tamarin | [Golden lion tamarin](https://en.wikipedia.org/wiki/Golden_lion_tamarin) | [history](https://en.wikipedia.org/w/index.php?title=Golden_lion_tamarin&action=history) |
| Golden Snub-nosed Monkey | [Golden snub-nosed monkey](https://en.wikipedia.org/wiki/Golden_snub-nosed_monkey) | [history](https://en.wikipedia.org/w/index.php?title=Golden_snub-nosed_monkey&action=history) |
| Golden Tanager | [Golden tanager](https://en.wikipedia.org/wiki/Golden_tanager) | [history](https://en.wikipedia.org/w/index.php?title=Golden_tanager&action=history) |
| Gray-shanked Douc | [Gray-shanked douc](https://en.wikipedia.org/wiki/Gray-shanked_douc) | [history](https://en.wikipedia.org/w/index.php?title=Gray-shanked_douc&action=history) |
| Green-headed Tanager | [Green-headed tanager](https://en.wikipedia.org/wiki/Green-headed_tanager) | [history](https://en.wikipedia.org/w/index.php?title=Green-headed_tanager&action=history) |
| Grizzly-Polar Bear Hybrid | to fill | to fill |
| Highlander | to fill ("Highlander" is a [disambiguation page](https://en.wikipedia.org/wiki/Highlander)) | to fill |
| Howler Monkey | [Howler monkey](https://en.wikipedia.org/wiki/Howler_monkey) | [history](https://en.wikipedia.org/w/index.php?title=Howler_monkey&action=history) |
| Indian Elephant | [Indian elephant](https://en.wikipedia.org/wiki/Indian_elephant) | [history](https://en.wikipedia.org/w/index.php?title=Indian_elephant&action=history) |
| Irish Terrier | [Irish Terrier](https://en.wikipedia.org/wiki/Irish_Terrier) | [history](https://en.wikipedia.org/w/index.php?title=Irish_Terrier&action=history) |
| Japanese Macaque | [Japanese macaque](https://en.wikipedia.org/wiki/Japanese_macaque) | [history](https://en.wikipedia.org/w/index.php?title=Japanese_macaque&action=history) |
| Kerry Beagle | [Kerry Beagle](https://en.wikipedia.org/wiki/Kerry_Beagle) | [history](https://en.wikipedia.org/w/index.php?title=Kerry_Beagle&action=history) |
| Mammoth | [Mammoth](https://en.wikipedia.org/wiki/Mammoth) | [history](https://en.wikipedia.org/w/index.php?title=Mammoth&action=history) |
| Mandrill | [Mandrill](https://en.wikipedia.org/wiki/Mandrill) | [history](https://en.wikipedia.org/w/index.php?title=Mandrill&action=history) |
| Mastodon | [Mastodon](https://en.wikipedia.org/wiki/Mastodon) | [history](https://en.wikipedia.org/w/index.php?title=Mastodon&action=history) |
| Norwegian Buhund | [Norwegian Buhund](https://en.wikipedia.org/wiki/Norwegian_Buhund) | [history](https://en.wikipedia.org/w/index.php?title=Norwegian_Buhund&action=history) |
| Paradise Tanager | [Paradise tanager](https://en.wikipedia.org/wiki/Paradise_tanager) | [history](https://en.wikipedia.org/w/index.php?title=Paradise_tanager&action=history) |
| Patterdale Terrier | [Patterdale Terrier](https://en.wikipedia.org/wiki/Patterdale_Terrier) | [history](https://en.wikipedia.org/w/index.php?title=Patterdale_Terrier&action=history) |
| Peterbald | [Peterbald](https://en.wikipedia.org/wiki/Peterbald) | [history](https://en.wikipedia.org/w/index.php?title=Peterbald&action=history) |
| Polar Bear | to fill ("Polar Bear" is a [disambiguation page](https://en.wikipedia.org/wiki/Polar_Bear)) | to fill |
| Proboscis Monkey | [Proboscis monkey](https://en.wikipedia.org/wiki/Proboscis_monkey) | [history](https://en.wikipedia.org/w/index.php?title=Proboscis_monkey&action=history) |
| Purple-faced Langur | [Purple-faced langur](https://en.wikipedia.org/wiki/Purple-faced_langur) | [history](https://en.wikipedia.org/w/index.php?title=Purple-faced_langur&action=history) |
| Pygmy Elephant | [Borneo elephant](https://en.wikipedia.org/wiki/Borneo_elephant) | [history](https://en.wikipedia.org/w/index.php?title=Borneo_elephant&action=history) |
| Pygmy Mammoth | [Pygmy mammoth](https://en.wikipedia.org/wiki/Pygmy_mammoth) | [history](https://en.wikipedia.org/w/index.php?title=Pygmy_mammoth&action=history) |
| Red Panda | to fill ("Red Panda" is a [disambiguation page](https://en.wikipedia.org/wiki/Red_Panda)) | to fill |
| Red-shanked Douc | [Red-shanked douc](https://en.wikipedia.org/wiki/Red-shanked_douc) | [history](https://en.wikipedia.org/w/index.php?title=Red-shanked_douc&action=history) |
| Saffron-crowned Tanager | [Saffron-crowned tanager](https://en.wikipedia.org/wiki/Saffron-crowned_tanager) | [history](https://en.wikipedia.org/w/index.php?title=Saffron-crowned_tanager&action=history) |
| Scottish Fold | [Scottish Fold](https://en.wikipedia.org/wiki/Scottish_Fold) | [history](https://en.wikipedia.org/w/index.php?title=Scottish_Fold&action=history) |
| Short-faced Bear | to fill | to fill |
| Sloth Bear | [Sloth bear](https://en.wikipedia.org/wiki/Sloth_bear) | [history](https://en.wikipedia.org/w/index.php?title=Sloth_bear&action=history) |
| Spectacled Bear | [Spectacled bear](https://en.wikipedia.org/wiki/Spectacled_bear) | [history](https://en.wikipedia.org/w/index.php?title=Spectacled_bear&action=history) |
| Sphynx | [Sphynx cat](https://en.wikipedia.org/wiki/Sphynx_cat) | [history](https://en.wikipedia.org/w/index.php?title=Sphynx_cat&action=history) |
| Squirrel Monkey | [Squirrel monkey](https://en.wikipedia.org/wiki/Squirrel_monkey) | [history](https://en.wikipedia.org/w/index.php?title=Squirrel_monkey&action=history) |
| Sri Lankan Elephant | [Sri Lankan elephant](https://en.wikipedia.org/wiki/Sri_Lankan_elephant) | [history](https://en.wikipedia.org/w/index.php?title=Sri_Lankan_elephant&action=history) |
| St. Bernard | to fill ("Saint Bernard" is a [disambiguation page](https://en.wikipedia.org/wiki/Saint_Bernard)) | to fill |
| Sumatran Elephant | [Sumatran elephant](https://en.wikipedia.org/wiki/Sumatran_elephant) | [history](https://en.wikipedia.org/w/index.php?title=Sumatran_elephant&action=history) |
| Sun Bear | [Sun bear](https://en.wikipedia.org/wiki/Sun_bear) | [history](https://en.wikipedia.org/w/index.php?title=Sun_bear&action=history) |
| Thomas's Langur | [Thomas's langur](https://en.wikipedia.org/wiki/Thomas%27s_langur) | [history](https://en.wikipedia.org/w/index.php?title=Thomas%27s_langur&action=history) |
| Tonkin Snub-nosed Monkey | [Tonkin snub-nosed monkey](https://en.wikipedia.org/wiki/Tonkin_snub-nosed_monkey) | [history](https://en.wikipedia.org/w/index.php?title=Tonkin_snub-nosed_monkey&action=history) |
<!-- articles:end -->

## Packages used by the samples

The samples (not the `Routeables` packages, which depend only on `Microsoft.Extensions.DependencyInjection`) are built with the packages below, restored from NuGet when they
are built. They are not stored in this repository. Each one keeps its own license, shown on its page of nuget.org: **read them again before you distribute a sample as a
binary**, which then contains these libraries.

| Package | License |
|---|---|
| Avalonia (and its `Avalonia.*` packages) | MIT, Copyright (c) AvaloniaUI OÜ. `Avalonia.Fonts.Inter` carries the Inter font, see Fonts above |
| FluentAvaloniaUI | MIT |
| AsyncImageLoader.Avalonia | MIT, Copyright (c) 2021 SKProCH |
| Svg.Controls.Skia.Avalonia | MIT, Copyright (c) 2020 Wiesław Šoltés |
| SharpVectors.Wpf | BSD 3-Clause |
| MudBlazor | MIT, Copyright (c) 2021 MudBlazor |
| CommunityToolkit.Maui, CommunityToolkit.Mvvm, Microsoft.Maui.Controls, Microsoft.AspNetCore.Components.\*, Microsoft.Extensions.\* | MIT, .NET Foundation and contributors |
| Uno.WinUI | Apache License 2.0 |
| SkiaSharp.NativeAssets.WebAssembly, Xamarin.AndroidX.Core.SplashScreen | MIT |

## Trademarks

.NET, Blazor, MAUI, Avalonia, Uno Platform, Prism, MudBlazor and the other product names of this repository belong to their owners. They name the products that Routeables
works with; they do not mean that those owners endorse it.

## Apache License 2.0


                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.

      "License" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      "Licensor" shall mean the copyright owner or entity authorized by
      the copyright owner that is granting the License.

      "Legal Entity" shall mean the union of the acting entity and all
      other entities that control, are controlled by, or are under common
      control with that entity. For the purposes of this definition,
      "control" means (i) the power, direct or indirect, to cause the
      direction or management of such entity, whether by contract or
      otherwise, or (ii) ownership of fifty percent (50%) or more of the
      outstanding shares, or (iii) beneficial ownership of such entity.

      "You" (or "Your") shall mean an individual or Legal Entity
      exercising permissions granted by this License.

      "Source" form shall mean the preferred form for making modifications,
      including but not limited to software source code, documentation
      source, and configuration files.

      "Object" form shall mean any form resulting from mechanical
      transformation or translation of a Source form, including but
      not limited to compiled object code, generated documentation,
      and conversions to other media types.

      "Work" shall mean the work of authorship, whether in Source or
      Object form, made available under the License, as indicated by a
      copyright notice that is included in or attached to the work
      (an example is provided in the Appendix below).

      "Derivative Works" shall mean any work, whether in Source or Object
      form, that is based on (or derived from) the Work and for which the
      editorial revisions, annotations, elaborations, or other modifications
      represent, as a whole, an original work of authorship. For the purposes
      of this License, Derivative Works shall not include works that remain
      separable from, or merely link (or bind by name) to the interfaces of,
      the Work and Derivative Works thereof.

      "Contribution" shall mean any work of authorship, including
      the original version of the Work and any modifications or additions
      to that Work or Derivative Works thereof, that is intentionally
      submitted to Licensor for inclusion in the Work by the copyright owner
      or by an individual or Legal Entity authorized to submit on behalf of
      the copyright owner. For the purposes of this definition, "submitted"
      means any form of electronic, verbal, or written communication sent
      to the Licensor or its representatives, including but not limited to
      communication on electronic mailing lists, source code control systems,
      and issue tracking systems that are managed by, or on behalf of, the
      Licensor for the purpose of discussing and improving the Work, but
      excluding communication that is conspicuously marked or otherwise
      designated in writing by the copyright owner as "Not a Contribution."

      "Contributor" shall mean Licensor and any individual or Legal Entity
      on behalf of whom a Contribution has been received by Licensor and
      subsequently incorporated within the Work.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
      Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      (except as stated in this section) patent license to make, have made,
      use, offer to sell, sell, import, and otherwise transfer the Work,
      where such license applies only to those patent claims licensable
      by such Contributor that are necessarily infringed by their
      Contribution(s) alone or by combination of their Contribution(s)
      with the Work to which such Contribution(s) was submitted. If You
      institute patent litigation against any entity (including a
      cross-claim or counterclaim in a lawsuit) alleging that the Work
      or a Contribution incorporated within the Work constitutes direct
      or contributory patent infringement, then any patent licenses
      granted to You under this License for that Work shall terminate
      as of the date such litigation is filed.

   4. Redistribution. You may reproduce and distribute copies of the
      Work or Derivative Works thereof in any medium, with or without
      modifications, and in Source or Object form, provided that You
      meet the following conditions:

      (a) You must give any other recipients of the Work or
          Derivative Works a copy of this License; and

      (b) You must cause any modified files to carry prominent notices
          stating that You changed the files; and

      (c) You must retain, in the Source form of any Derivative Works
          that You distribute, all copyright, patent, trademark, and
          attribution notices from the Source form of the Work,
          excluding those notices that do not pertain to any part of
          the Derivative Works; and

      (d) If the Work includes a "NOTICE" text file as part of its
          distribution, then any Derivative Works that You distribute must
          include a readable copy of the attribution notices contained
          within such NOTICE file, excluding those notices that do not
          pertain to any part of the Derivative Works, in at least one
          of the following places: within a NOTICE text file distributed
          as part of the Derivative Works; within the Source form or
          documentation, if provided along with the Derivative Works; or,
          within a display generated by the Derivative Works, if and
          wherever such third-party notices normally appear. The contents
          of the NOTICE file are for informational purposes only and
          do not modify the License. You may add Your own attribution
          notices within Derivative Works that You distribute, alongside
          or as an addendum to the NOTICE text from the Work, provided
          that such additional attribution notices cannot be construed
          as modifying the License.

      You may add Your own copyright statement to Your modifications and
      may provide additional or different license terms and conditions
      for use, reproduction, or distribution of Your modifications, or
      for any such Derivative Works as a whole, provided Your use,
      reproduction, and distribution of the Work otherwise complies with
      the conditions stated in this License.

   5. Submission of Contributions. Unless You explicitly state otherwise,
      any Contribution intentionally submitted for inclusion in the Work
      by You to the Licensor shall be under the terms and conditions of
      this License, without any additional terms or conditions.
      Notwithstanding the above, nothing herein shall supersede or modify
      the terms of any separate license agreement you may have executed
      with Licensor regarding such Contributions.

   6. Trademarks. This License does not grant permission to use the trade
      names, trademarks, service marks, or product names of the Licensor,
      except as required for reasonable and customary use in describing the
      origin of the Work and reproducing the content of the NOTICE file.

   7. Disclaimer of Warranty. Unless required by applicable law or
      agreed to in writing, Licensor provides the Work (and each
      Contributor provides its Contributions) on an "AS IS" BASIS,
      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
      implied, including, without limitation, any warranties or conditions
      of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
      PARTICULAR PURPOSE. You are solely responsible for determining the
      appropriateness of using or redistributing the Work and assume any
      risks associated with Your exercise of permissions under this License.

   8. Limitation of Liability. In no event and under no legal theory,
      whether in tort (including negligence), contract, or otherwise,
      unless required by applicable law (such as deliberate and grossly
      negligent acts) or agreed to in writing, shall any Contributor be
      liable to You for damages, including any direct, indirect, special,
      incidental, or consequential damages of any character arising as a
      result of this License or out of the use or inability to use the
      Work (including but not limited to damages for loss of goodwill,
      work stoppage, computer failure or malfunction, or any and all
      other commercial damages or losses), even if such Contributor
      has been advised of the possibility of such damages.

   9. Accepting Warranty or Additional Liability. While redistributing
      the Work or Derivative Works thereof, You may choose to offer,
      and charge a fee for, acceptance of support, warranty, indemnity,
      or other liability obligations and/or rights consistent with this
      License. However, in accepting such obligations, You may act only
      on Your own behalf and on Your sole responsibility, not on behalf
      of any other Contributor, and only if You agree to indemnify,
      defend, and hold each Contributor harmless for any liability
      incurred by, or claims asserted against, such Contributor by reason
      of your accepting any such warranty or additional liability.

   END OF TERMS AND CONDITIONS

   APPENDIX: How to apply the Apache License to your work.

      To apply the Apache License to your work, attach the following
      boilerplate notice, with the fields enclosed by brackets "[]"
      replaced with your own identifying information. (Don't include
      the brackets!)  The text should be enclosed in the appropriate
      comment syntax for the file format. We also recommend that a
      file or class name and description of purpose be included on the
      same "printed page" as the copyright notice for easier
      identification within third-party archives.

   Copyright [yyyy] [name of copyright owner]

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
