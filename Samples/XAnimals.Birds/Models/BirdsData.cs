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
                new Animal
                {
                    Name = "Green-headed Tanager",
                    Location = "Atlantic Forest of Brazil, Paraguay & Argentina",
                    Details = "The green-headed tanager lives in the Atlantic forest of southeastern Brazil, far eastern Paraguay and, in Argentina, the province of Misiones only. It is found mainly in humid forest, but it is also common in orchards and parks, where it moves through the canopy. It is about 13.5 cm long and weighs about 18 g. It has a greenish or bluish head, a black back and a contrasting orange or red rump. Its blue-green colors help it blend into the foliage, so it is less conspicuous than it looks. Females and young birds are duller.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/d/d1/Green-headed_Tanager_%28Tangara_seledon%29_%288077715480%29.jpg"
                },
                new Animal
                {
                    Name = "Golden Tanager",
                    Location = "Andes & Venezuelan Coastal Range",
                    Details = "The golden tanager lives in the highland forests of the Andes, from Bolivia northward, and in the Coastal Range of Venezuela. It occurs at elevations of roughly 700 to 2,500 meters and is most common between 1,000 and 1,500 meters. It prefers humid montane evergreen forest, and it also uses forest edges and nearby secondary growth. Most of the bird is golden-yellow, with black on the back, the wings, the tail and the ear coverts. Some subspecies are partly or mostly brown on the underparts.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/2/26/Tangara_arthus_-_T%C3%A1ngara_dorada_-_Golden_Tanager_%288542820434%29.jpg"
                },
                new Animal
                {
                    Name = "Saffron-crowned Tanager",
                    Location = "Northern Andes",
                    Details = "The saffron-crowned tanager lives in the northern Andes, in Bolivia, Colombia, Ecuador, Peru and Venezuela, at elevations of roughly 1,000 to 2,700 meters. It favors cloud forests, forest edges and regrowth woodland, and is especially drawn to mossy trees. It is about 12.5 to 13.5 cm long and weighs 15 to 23.6 g. Fruit makes up most of its diet, more than in any other species of the genus Tangara: it eats Cecropia, Miconia and Morus, and takes insects, often from mossy branches. Adults have a blue-green body and a yellow head, with black on the forehead, around the eyes and on the chin. Juveniles are duller and greener.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/a/a7/Saffron-crowned_Tanager_%28Tangara_xanthocephala%29_%28cropped%29.jpg"
                }
            ];
        }
    }
}
