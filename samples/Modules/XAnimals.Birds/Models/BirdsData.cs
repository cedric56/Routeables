using XAminals.Models;

namespace XAnimals.Birds
{
    /// <summary>The tanagers of the genus Tangara, the birds of the Birds module: the same kind of list as the bears, the cats and the others.</summary>
    public static class BirdsData
    {
        public static IList<Animal> Birds { get; private set; }

        static BirdsData()
        {
            Birds =
            [
                new Animal
                {
                    Name = "Paradise Tanager",
                    Location = "Amazon Basin, South America",
                    Details = "The paradise tanager is a medium-sized songbird, 13.5 to 15 cm long, of the humid tropical and subtropical forests of the western and northern Amazon Basin, in Venezuela, Colombia, Ecuador, Peru, Bolivia and Brazil. Despite its scientific name, Tangara chilensis, it does not live in Chile. It has a light green head, sky blue underparts and a black back, with a yellow and red or all red rump, depending on the subspecies. Its brilliant colors come in part from layers of black or white pigment under the feathers, which make the structural colors stronger.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/4/4e/Paradise_Tanager_Woodland_Parks_Zoo_RWD.jpg"
                },
                new Animal
                {
                    Name = "Bay-headed Tanager",
                    Location = "Central & South America",
                    Details = "The bay-headed tanager is a resident breeder in Costa Rica and Panama, across much of South America south to Ecuador, Bolivia and northwestern Brazil, and on Trinidad. It lives in forests, especially the wetter ones, and is a social bird. Adults are about 14 cm long and weigh about 19.5 g. Most of the body is green, with a chestnut head, a blue or green belly and a thin gold collar on the back of the neck. It eats mostly fruit, which it swallows whole, and takes insects, mainly from the undersides of branches. Its plumage varies among nine subspecies.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/f/f7/Bay-headed_Tanager.jpg"
                },
                new Animal
                {
                    Name = "Burnished-buff Tanager",
                    Location = "Northern & eastern South America",
                    Details = "The burnished-buff tanager is found in the Guianas, most of Venezuela, east-central Colombia, eastern Brazil, Paraguay and northeastern Argentina. It can be seen in virtually any semi-open habitat with trees, including gardens, plantations and parks. It is mainly a fruit eater, with a fondness for the fruit of native Cecropia and Brazilian pepper. Males have turquoise wings and tail. Their crown, mask and underparts vary by group: a rufous crown, a black mask and cream underparts in the cayana group, a buff crown and buff underparts with a black patch on the throat in the flava group. Females are duller.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/1/1c/Tangara_cayana_Tangara_Triguera_Burnished-buff_Tanager_%2815792282823%29_%28cropped%29.jpg"
                },
                new Animal
                {
                    Name = "Blue-necked Tanager",
                    Location = "Colombia to Bolivia",
                    Details = "The blue-necked tanager lives from Colombia to Bolivia, in moist lowland forests, moist montane forests and heavily degraded former forest. It is about 12 cm long and weighs roughly 17 g. Fruit is its main food, with berries, flower blossoms and insects picked from leaves or caught in flight. It is recognized by its blue hood and throat. The wing coverts, the wing edges and the rump are turquoise to greenish straw, and the underparts toward the rear are black, violet or blue. Juveniles are brownish gray with a hint of the adult colors.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/4/44/Blue-necked_Tanager_%28Tangara_cyanicollis%29_%284856996470%29.jpg"
                },
            ];
        }
    }
}
