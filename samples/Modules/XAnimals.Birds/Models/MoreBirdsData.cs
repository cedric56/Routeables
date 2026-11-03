using XAminals.Models;

namespace XAnimals.Birds
{
    /// <summary>The tanagers of the module loaded while the application runs (see <see cref="MoreBirdsModule"/>): three more species of the genus Tangara.</summary>
    public static class MoreBirdsData
    {
        public static IList<Animal> Birds { get; private set; }

        static MoreBirdsData()
        {
            Birds =
            [
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
