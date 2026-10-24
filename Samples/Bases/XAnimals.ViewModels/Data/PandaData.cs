using XAminals.Models;

namespace Xaminals.Data
{
    public static class PandaData
    {
        public static IList<Animal> Pandas { get; private set; }

        static PandaData()
        {
            Pandas =
            [
                new Animal
                {
                    Name = "Giant Panda",
                    Location = "Central China",
                    Details = "The giant panda is a bear native to the mountains of central China. It is easily recognised by its large, round body and its black and white coat, with black patches around the eyes, on the ears, and on the legs and shoulders. Although it belongs to the order Carnivora, its diet is almost entirely bamboo, of which an adult can eat more than ten kilograms a day. It is a vulnerable species, and is the national symbol of China.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/8/8d/Lightmatter_panda.jpg"
                },
                new Animal
                {
                    Name = "Red Panda",
                    Location = "Himalayas & Southwest China",
                    Details = "The red panda is a small mammal native to the eastern Himalayas and southwestern China. It has dense reddish-brown fur, a long ringed tail, and a waddling gait due to its shorter front legs. It spends most of its life in trees and feeds mainly on bamboo. Despite its name, it is not closely related to the giant panda: it is the only living species of its own family, and it is endangered.",
                    ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/e/e6/Red_Panda_(24986761703).jpg"
                },
            ];
        }
    }
}
