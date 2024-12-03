using System;
using System.Collections.Generic;

namespace TimHanewich.Toolkit
{
    public class RandomNameGenerator
    {

        private string[] MaleFirstNames { get; set; }
        private string[] FemaleFirstNames { get; set; }
        private string[] LastNames { get; set; }
        private Random rnd = new Random();

        public RandomNameGenerator()
        {
            List<string> Splitter = new List<string>();
            Splitter.Add(",");
            MaleFirstNames = "Noah,Liam,Jacob,Mason,William,Ethan,Michael,Alexander,James,Elijah,Daniel,Benjamin,Aiden,Jayden,Logan,Matthew,David,Joseph,Lucas,Jackson,Anthony,Joshua,Samuel,Andrew,Gabriel,Christopher,John,Dylan,Carter,Isaac,Ryan,Luke,Oliver,Nathan,Henry,Owen,Caleb,Wyatt,Christian,Sebastian,Jack,Jonathan,Landon,Julian,Isaiah,Hunter,Levi,Aaron,Eli,Charles,Thomas,Connor,Brayden,Nicholas,Jaxon,Jeremiah,Cameron,Evan,Adrian,Jordan,Gavin,Grayson,Angel,Robert,Tyler,Josiah,Austin,Colton,Brandon,Jose,Dominic,Kevin,Zachary,Ian,Chase,Jason,Adam,Ayden,Parker,Hudson,Cooper,Nolan,Lincoln,Xavier,Carson,Jace,Justin,Easton,Mateo,Asher,Bentley,Blake,Nathaniel,Jaxson,Leo,Kayden,Tristan,Luis,Elias,Brody,Bryson,Juan,Vincent,Cole,Micah,Ryder,Theodore,Carlos,Ezra,Damian,Miles,Santiago,Max,Jesus,Leonardo,Sawyer,Diego,Alex,Roman,Maxwell,Eric,Greyson,Hayden,Giovanni,Wesley,Axel,Camden,Braxton,Ivan,Ashton,Declan,Bryce,Timothy,Antonio,Silas,Kaiden,Ezekiel,Jonah,Weston,George,Harrison,Steven,Miguel,Richard,Bryan,Kaleb,Victor,Aidan,Jameson,Joel,Patrick,Jaden,Colin,Everett,Preston,Maddox,Edward,Alejandro,Kaden,Jesse,Emmanuel,Kyle,Brian,Emmett,Jude,Marcus,Kingston,Kai,Alan,Malachi,Grant,Jeremy,Riley,Jayce,Bennett,Abel,Ryker,Caden,Brantley,Luca,Brady,Calvin,Sean,Oscar,Jake,Maverick,Abraham,Mark,Tucker,Nicolas,Bradley,Kenneth,Avery,Cayden,King,Paul,Amir,Gael,Graham,Maximus".Split(Splitter.ToArray(), StringSplitOptions.None);
            FemaleFirstNames = "Emma,Sophia,Olivia,Isabella,Ava,Mia,Abigail,Emily,Madison,Charlotte,Elizabeth,Amelia,Chloe,Ella,Evelyn,Avery,Sofia,Harper,Grace,Addison,Victoria,Natalie,Lily,Aubrey,Lillian,Zoey,Hannah,Layla,Brooklyn,Samantha,Zoe,Leah,Scarlett,Riley,Camila,Savannah,Anna,Audrey,Allison,Aria,Gabriella,Hailey,Claire,Sarah,Aaliyah,Kaylee,Nevaeh,Penelope,Alexa,Arianna,Stella,Alexis,Bella,Nora,Ellie,Ariana,Lucy,Mila,Peyton,Genesis,Alyssa,Taylor,Violet,Maya,Caroline,Madelyn,Skylar,Serenity,Ashley,Brianna,Kennedy,Autumn,Eleanor,Kylie,Sadie,Paisley,Julia,Mackenzie,Sophie,Naomi,Eva,Khloe,Katherine,Gianna,Melanie,Aubree,Piper,Ruby,Lydia,Faith,Madeline,Alexandra,Kayla,Hazel,Lauren,Annabelle,Jasmine,Aurora,Alice,Makayla,Sydney,Bailey,Luna,Maria,Reagan,Morgan,Isabelle,Rylee,Kimberly,Andrea,London,Elena,Jocelyn,Natalia,Trinity,Eliana,Vivian,Cora,Quinn,Liliana,Molly,Jade,Clara,Valentina,Mary,Brielle,Hadley,Kinsley,Willow,Brooke,Lilly,Delilah,Payton,Mariah,Paige,Jordyn,Nicole,Mya,Josephine,Isabel,Lyla,Adeline,Destiny,Ivy,Emilia,Rachel,Angelina,Valeria,Kendall,Sara,Ximena,Isla,Aliyah,Reese,Vanessa,Juliana,Mckenzie,Amy,Laila,Adalynn,Emery,Margaret,Eden,Gabrielle,Kaitlyn,Ariel,Gracie,Brooklynn,Melody,Jessica,Valerie,Adalyn,Adriana,Elise,Michelle,Rebecca,Daisy,Everly,Katelyn,Ryleigh,Catherine,Norah,Alaina,Athena,Leilani,Londyn,Eliza,Jayla,Summer,Lila,Makenzie,Izabella,Daniela,Stephanie,Julianna,Rose,Alana,Harmony,Jennifer,Hayden".Split(Splitter.ToArray(), StringSplitOptions.None);
            LastNames = "Smith,Johnson,Williams,Jones,Brown,Davis,Miller,Wilson,Moore,Taylor,Anderson,Thomas,Jackson,White,Harris,Martin,Thompson,Garcia,Martinez,Robinson,Clark,Rodriguez,Lewis,Lee,Walker,Hall,Allen,Young,Hernandez,King,Wright,Lopez,Hill,Scott,Green,Adams,Baker,Gonzalez,Nelson,Carter,Mitchell,Perez,Roberts,Turner,Phillips,Campbell,Parker,Evans,Edwards,Collins,Stewart,Sanchez,Morris,Rogers,Reed,Cook,Morgan,Bell,Murphy,Bailey,Rivera,Cooper,Richardson,Cox,Howard,Ward,Torres,Peterson,Gray,Ramirez,James,Watson,Brooks,Kelly,Sanders,Price,Bennett,Wood,Barnes,Ross,Henderson,Coleman,Jenkins,Perry,Powell,Long,Patterson,Hughes,Flores,Washington,Butler,Simmons,Foster,Gonzales,Bryant,Alexander,Russell,Griffin,Diaz,Hayes,Myers,Ford,Hamilton,Graham,Sullivan,Wallace,Woods,Cole,West,Jordan,Owens,Reynolds,Fisher,Ellis,Harrison,Gibson,Mcdonald,Cruz,Marshall,Ortiz,Gomez,Murray,Freeman,Wells,Webb,Simpson,Stevens,Tucker,Porter,Hunter,Hicks,Crawford,Henry,Boyd,Mason,Morales,Kennedy,Warren,Dixon,Ramos,Reyes,Burns,Gordon,Shaw,Holmes,Rice,Robertson,Hunt,Black,Daniels,Palmer,Mills,Nichols,Grant,Knight,Ferguson,Rose,Stone,Hawkins,Dunn,Perkins,Hudson,Spencer,Gardner,Stephens,Payne,Pierce,Berry,Matthews,Arnold,Wagner,Willis,Ray,Watkins,Olson,Carroll,Duncan,Snyder,Hart,Cunningham,Bradley,Lane,Andrews,Ruiz,Harper,Fox,Riley,Armstrong,Carpenter,Weaver,Greene,Lawrence,Elliott,Chavez,Sims,Austin,Peters,Kelley,Franklin,Lawson,Fields,Gutierrez,Ryan,Schmidt,Carr,Vasquez,Castillo,Wheeler,Chapman,Oliver,Montgomery,Richards,Williamson,Johnston,Banks,Meyer,Bishop,Mccoy,Howell,Alvarez,Morrison,Hansen,Fernandez,Garza,Harvey,Little,Burton,Stanley,Nguyen,George,Jacobs,Reid,Kim,Fuller,Lynch,Dean,Gilbert,Garrett,Romero,Welch,Larson,Frazier,Burke,Hanson,Day,Mendoza,Moreno,Bowman,Medina,Fowler,Brewer,Hoffman,Carlson,Silva,Pearson,Holland,Douglas,Fleming,Jensen,Vargas,Byrd,Davidson,Hopkins,May,Terry,Herrera,Wade,Soto,Walters,Curtis,Neal,Caldwell,Lowe,Jennings,Barnett,Graves,Jimenez,Horton,Shelton,Barrett,Obrien,Castro,Sutton,Gregory,Mckinney,Lucas,Miles,Craig,Rodriquez,Chambers,Holt,Lambert,Fletcher,Watts,Bates,Hale,Rhodes,Pena,Beck,Newman,Haynes,Mcdaniel,Mendez,Bush,Vaughn,Parks,Dawson,Santiago,Norris,Hardy,Love,Steele,Curry,Powers,Schultz,Barker,Guzman,Page,Munoz,Ball,Keller,Chandler,Weber,Leonard,Walsh,Lyons,Ramsey,Wolfe,Schneider,Mullins,Benson,Sharp,Bowen,Daniel,Barber,Cummings,Hines,Baldwin,Griffith,Valdez,Hubbard,Salazar,Reeves,Warner,Stevenson,Burgess,Santos,Tate,Cross,Garner,Mann,Mack,Moss,Thornton,Dennis,Mcgee,Farmer,Delgado,Aguilar,Vega,Glover,Manning,Cohen,Harmon,Rodgers,Robbins,Newton,Todd,Blair,Higgins,Ingram,Reese,Cannon,Strickland,Townsend,Potter,Goodwin,Walton,Rowe,Hampton,Ortega,Patton,Swanson,Joseph,Francis,Goodman,Maldonado,Yates,Becker,Erickson,Hodges,Rios,Conner,Adkins,Webster,Norman,Malone,Hammond,Flowers,Cobb,Moody,Quinn,Blake,Maxwell,Pope,Floyd,Osborne,Paul,Mccarthy,Guerrero,Lindsey,Estrada,Sandoval,Gibbs,Tyler,Gross,Fitzgerald,Stokes,Doyle,Sherman,Saunders,Wise,Colon,Gill,Alvarado,Greer,Padilla,Simon,Waters,Nunez,Ballard,Schwartz,Mcbride,Houston,Christensen,Klein,Pratt,Briggs,Parsons,Mclaughlin,Zimmerman,French,Buchanan,Moran,Copeland,Roy,Pittman,Brady,Mccormick,Holloway,Brock,Poole,Frank,Logan,Owen,Bass,Marsh,Drake,Wong,Jefferson,Park,Morton,Abbott,Sparks,Patrick,Norton,Huff,Clayton,Massey,Lloyd,Figueroa,Carson,Bowers,Roberson,Barton,Tran,Lamb,Harrington,Casey,Boone,Cortez,Clarke,Mathis,Singleton,Wilkins,Cain,Bryan,Underwood,Hogan,Mckenzie,Collier,Luna,Phelps,Mcguire,Allison,Bridges,Wilkerson,Nash,Summers,Atkins,Wilcox,Pitts,Conley,Marquez,Burnett,Richard,Cochran,Chase,Davenport,Hood,Gates,Clay,Ayala,Sawyer,Roman,Vazquez,Dickerson,Hodge,Acosta,Flynn,Espinoza,Nicholson,Monroe,Wolf,Morrow,Kirk,Randall,Anthony,Whitaker,Oconnor,Skinner,Ware,Molina,Kirby,Huffman,Bradford,Charles,Gilmore,Dominguez,Oneal,Bruce,Lang,Combs,Kramer,Heath,Hancock,Gallagher,Gaines,Shaffer,Short,Wiggins,Mathews,Mcclain,Fischer,Wall,Small,Melton,Hensley,Bond,Dyer,Cameron,Grimes,Contreras,Christian,Wyatt,Baxter,Snow,Mosley,Shepherd,Larsen,Hoover,Beasley,Glenn,Petersen,Whitehead,Meyers,Keith,Garrison,Vincent,Shields,Horn,Savage,Olsen,Schroeder,Hartman,Woodard,Mueller,Kemp,Deleon,Booth,Patel,Calhoun,Wiley,Eaton,Cline,Navarro,Harrell,Lester,Humphrey,Parrish,Duran,Hutchinson,Hess,Dorsey,Bullock,Robles,Beard,Dalton,Avila,Vance,Rich,Blackwell,York,Johns,Blankenship,Trevino,Salinas,Campos,Pruitt,Moses,Callahan,Golden,Montoya,Hardin,Guerra,Mcdowell,Carey,Stafford,Gallegos,Henson,Wilkinson,Booker,Merritt,Miranda,Atkinson,Orr,Decker,Hobbs,Preston,Tanner,Knox,Pacheco,Stephenson,Glass,Rojas,Serrano,Marks,Hickman,English,Sweeney,Strong,Prince,Mcclure,Conway,Walter,Roth,Maynard,Farrell,Lowery,Hurst,Nixon,Weiss,Trujillo,Ellison,Sloan,Juarez,Winters,Mclean,Randolph,Leon,Boyer,Villarreal,Mccall,Gentry,Carrillo,Kent,Ayers,Lara,Shannon,Sexton,Pace,Hull,Leblanc,Browning,Velasquez,Leach,Chang,House,Sellers,Herring,Noble,Foley,Bartlett,Mercado,Landry,Durham,Walls,Barr,Mckee,Bauer,Rivers,Everett,Bradshaw,Pugh,Velez,Rush,Estes,Dodson,Morse,Sheppard,Weeks,Camacho,Bean,Barron,Livingston,Middleton,Spears,Branch,Blevins,Chen,Kerr,Mcconnell,Hatfield,Harding,Ashley,Solis,Herman,Frost,Giles,Blackburn,William,Pennington,Woodward,Finley,Mcintosh,Koch,Best,Solomon,Mccullough,Dudley,Nolan,Blanchard,Rivas,Brennan,Mejia,Kane,Benton,Joyce,Buckley,Haley,Valentine,Maddox,Russo,Mcknight,Buck,Moon,Mcmillan,Crosby,Berg,Dotson,Mays,Roach,Church,Chan,Richmond,Meadows,Faulkner,Oneill,Knapp,Kline,Barry,Ochoa,Jacobson,Gay,Avery,Hendricks,Horne,Shepard,Hebert,Cherry,Cardenas,Mcintyre,Whitney,Waller,Holman,Donaldson,Cantu,Terrell,Morin,Gillespie,Fuentes,Tillman,Sanford,Bentley,Peck,Key,Salas,Rollins,Gamble,Dickson,Battle,Santana,Cabrera,Cervantes,Howe,Hinton,Hurley,Spence,Zamora,Yang,Mcneil,Suarez,Case,Petty,Gould,Mcfarland,Sampson,Carver,Bray,Rosario,Macdonald,Stout,Hester,Melendez,Dillon,Farley,Hopper,Galloway,Potts,Bernard,Joyner,Stein,Aguirre,Osborn,Mercer,Bender,Franco,Rowland,Sykes,Benjamin,Travis,Pickett,Crane,Sears,Mayo,Dunlap,Hayden,Wilder,Mckay,Coffey,Mccarty,Ewing,Cooley,Vaughan,Bonner,Cotton,Holder,Stark,Ferrell,Cantrell,Fulton,Lynn,Lott,Calderon,Rosa,Pollard,Hooper,Burch,Mullen,Fry,Riddle,Levy,David,Duke,Odonnell,Guy,Michael,Britt,Frederick,Daugherty,Berger,Dillard,Alston,Jarvis,Frye,Riggs,Chaney,Odom,Duffy,Fitzpatrick,Valenzuela,Merrill,Mayer,Alford,Mcpherson,Acevedo,Donovan,Barrera,Albert,Cote,Reilly,Compton,Raymond,Mooney,Mcgowan,Craft,Cleveland,Clemons,Wynn,Nielsen,Baird,Stanton,Snider,Rosales,Bright,Witt,Stuart,Hays,Holden,Rutledge,Kinney,Clements,Castaneda,Slater,Hahn,Emerson,Conrad,Burks,Delaney,Pate,Lancaster,Sweet,Justice,Tyson,Sharpe,Whitfield,Talley,Macias,Irwin,Burris,Ratliff,Mccray,Madden,Kaufman,Beach,Goff,Cash,Bolton,Mcfadden,Levine,Good,Byers,Kirkland,Kidd,Workman,Carney,Dale,Mcleod,Holcomb,England,Finch,Head,Burt,Hendrix,Sosa,Haney,Franks,Sargent,Nieves,Downs,Rasmussen,Bird,Hewitt,Lindsay,Le,Foreman,Valencia,Oneil,Delacruz,Vinson,Dejesus,Hyde,Forbes,Gilliam,Guthrie,Wooten,Huber,Barlow,Boyle,Mcmahon,Buckner,Rocha,Puckett,Langley,Knowles,Cooke,Velazquez,Whitley,Noel,Vang".Split(Splitter.ToArray(), StringSplitOptions.None);
        }

        public string GenerateMaleFirstName()
        {
            return MaleFirstNames[rnd.Next(0, MaleFirstNames.Length - 1)];
        }

        public string GenerateFemaleFirstName()
        {
            return FemaleFirstNames[rnd.Next(0, FemaleFirstNames.Length - 1)];
        }

        public string GenerateLastName()
        {
            return LastNames[rnd.Next(0, LastNames.Length - 1)];
        }

        public string GenerateMaleName()
        {
            string first_name = GenerateMaleFirstName();
            string last_name = GenerateLastName();
            return first_name + " " + last_name;
        }

        public string GenerateFemaleName()
        {
            string first_name = GenerateFemaleFirstName();
            string last_name = GenerateLastName();
            return first_name + " " + last_name;
        }

        public string GenerateName()
        {
            if (rnd.Next(1, 100) > 50)
            {
                return GenerateMaleName();
            }
            else
            {
                return GenerateFemaleName();
            }
        }

        public string GenerateFirstName()
        {
            if (rnd.Next(1, 100) > 50)
            {
                return GenerateMaleFirstName();
            }
            else
            {
                return GenerateFemaleFirstName();
            }
        }

    }
}