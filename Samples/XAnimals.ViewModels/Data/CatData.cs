using XAminals.Models;

namespace Xaminals.Data
{
    public static class CatData
    {
        public static IList<Animal> Cats { get; private set; }

        static CatData()
        {
            Cats =
            [
                new Animal
                {
                    Name = "Abyssinian",
                    Location = "Ethopia",
                    Details = "The Abyssinian is a breed of domestic short-haired cat with a distinctive tickedtabby coat, in which individual hairs are banded with different colors. The breed is named for Abyssinia (now called Ethiopia), where it is believed to have originated.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/9/9b/Gustav_chocolate.jpg"
                },
                new Animal
                {
                    Name = "Arabian Mau",
                    Location = "Arabian Peninsula",
                    Details = "The Arabian Mau is a formal breed of domestic cat, originated from the desert cat, a short-haired landrace native to the desert of the Arabian Peninsula. It lives there in the streets and has adapted very well to the extreme climate. The Arabian Mau is recognized as a formal breed by few fancier and breeder organization and cat registry, World Cat Federation (WCF) and Emirates Feline Federation (EFF). Based on one landrace, the Arabian Mau is a natural breed.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/d/d3/Bex_Arabian_Mau.jpg"
                },
                new Animal
                {
                    Name = "Bengal",
                    Location = "Asia",
                    Details = "The Bengal cat is a domesticated cat breed created from hybrids of domestic cats and the Asian leopard cat – the breed name comes from the taxonomic name. Back-crossing to domestic cats is then done with the goal of creating a healthy, and docile cat with wild-looking, high-contrast coat. Bengals have a wild appearance and may show spots, rosettes, arrowhead markings, or marbling.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/b/ba/Paintedcats_Red_Star_standing.jpg"
                },
                new Animal
                {
                    Name = "Burmese",
                    Location = "Thailand",
                    Details = "The Burmese cat is a breed of domestic cat, originating in Thailand, believed to have its roots near the present Thai-Burma border and developed in the United States and Britain.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/0/04/Blissandlucky11.jpg"
                },
                new Animal
                {
                    Name = "Cyprus",
                    Location = "Cyprus",
                    Details = "Cyprus cats, also known as Cypriot cats, Saint Helen cats, and Saint Nicholas cats, are a landrace of domestic cat found across the island of Cyprus. A standardized breed is being developed from them; among cat fancier and breeder organizations, it is presently fully recognized by the World Cat Federation (WCF), with breeding regulated by the World Cat Congress (WCC), under the name Aphrodite's Giant; and provisionally by The International Cat Association (TICA) as the Aphrodite. All three organizations permit shorthaired and semi-longhaired versions and no out-crossing to other breeds.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/b/b9/CyprusShorthair.jpg"
                },
                new Animal
                {
                    Name = "German Rex",
                    Location = "Germany",
                    Details = "The German Rex is a medium-sized breed with slender legs of a medium length. The head is round with well-developed cheeks and large, open ears. The eyes are of medium size in colours related to the coat colour. The coat is silky and short with a tendency to curl. The whiskers also curl, though less strongly than in the Cornish Rex. They may be nearly straight. All colours of coat, including white, are allowed. The body development is heavier than in the Cornish Rex - more like the European Shorthairs.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/c/c7/German_rex_harry_%28cropped%29.jpg"
                },
                new Animal
                {
                    Name = "Highlander",
                    Location = "United States",
                    Details = "The Highlander (also known as the Highlander Shorthair, and originally as the Highland Lynx), is an experimental breed of cat. The unique appearance of the Highlander comes from the deliberate cross between the Desert Lynx and the Jungle Curl breeds, also recently developed. The latter of these has some non-domestic ancestry from two Asian small cat species, the leopard cat and jungle cat, making the Highlander nominally a feline hybrid, though its foundation stock is mostly domestic cat.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/1/15/Highlander-7.jpg"
                },
                new Animal
                {
                    Name = "Peterbald",
                    Location = "Russia",
                    Details = "The Peterbald is a cat breed of Russian origin. It was created in St Petersburg in 1994 from an experimental breeding by Olga S. Mironova. They resemble Oriental Shorthairs with a hair-losing gene. The breed was accepted for Championship class competition in 2009.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/c/c7/Peterbald_male_Shango_by_Irina_Polunina.jpg"
                },
                new Animal
                {
                    Name = "Scottish Fold",
                    Location = "Scotland",
                    Details = "The Scottish Fold is a breed of domestic cat with a natural dominant-gene mutation that affects cartilage throughout the body, causing the ears to fold, bending forward and down towards the front of the head, which gives the cat what is often described as an owl-like appearance.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/5/5d/Adult_Scottish_Fold.jpg"
                },
                new Animal
                {
                    Name = "Sphynx",
                    Location = "Europe",
                    Details = "The Sphynx cat is a breed of cat known for its lack of coat (fur). It was developed through selective breeding, starting in the 1960s. The skin should have the texture of chamois, as it has fine hairs, or they may be completely hairless. Whiskers may be present, either whole or broken, or may be totally absent. They also have a narrow, long head, and webbed feet. Their skin is the color that their fur would be, and all the usual cat markings (solid, point, van, tabby, tortie, etc.) may be found on the Sphynx cat's skin. Because they have no coat, they lose more body heat than coated cats. This makes them warm to the touch as well as heat-seeking.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/e/e8/Sphinx2_July_2006.jpg"
                },
            ];
        }
    }
}