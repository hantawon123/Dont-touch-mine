using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// English names for the item labels authored in the item catalogue.
    /// </summary>
    /// <remarks>
    /// The catalogue asset carries one Korean label per item, and artists keep
    /// adding to it, so the translation lives beside the label rather than in
    /// the asset: a new item still shows up, in Korean, without anyone having
    /// to fill a second field before it can be played.
    /// <para>
    /// Keyed by the Korean label rather than the item id. Ids are opaque
    /// hashes and several items share one name (two milk cartons, two
    /// avocados), so the label is both the readable key and the one that only
    /// has to be translated once.
    /// </para>
    /// </remarks>
    public static class ItemNameText
    {
        private static readonly Dictionary<string, string> English =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AK74소총"] = "AK-74 Rifle",
                ["M107저격소총"] = "M107 Sniper Rifle",
                ["M1911권총"] = "M1911 Pistol",
                ["M249기관총"] = "M249 Machine Gun",
                ["M2중기관총"] = "M2 Heavy Machine Gun",
                ["M4소총"] = "M4 Rifle",
                ["RPG7발사기"] = "RPG-7 Launcher",
                ["TV장"] = "TV Stand",
                ["가마솥"] = "Cauldron",
                ["가스통"] = "Gas Cylinder",
                ["갈퀴"] = "Rake",
                ["감자칩"] = "Potato Chips",
                ["거미줄"] = "Cobweb",
                ["검"] = "Sword",
                ["고기"] = "Meat",
                ["고무오리"] = "Rubber Duck",
                ["고무장갑"] = "Rubber Gloves",
                ["고블릿"] = "Goblet",
                ["골드바"] = "Gold Bar",
                ["과일 접시"] = "Fruit Plate",
                ["관"] = "Coffin",
                ["구급상자"] = "First Aid Kit",
                ["구급용품"] = "Medical Supplies",
                ["국자"] = "Ladle",
                ["권총"] = "Pistol",
                ["그릇"] = "Bowl",
                ["금고"] = "Safe",
                ["금고 문"] = "Safe Door",
                ["낫"] = "Sickle",
                ["냄비"] = "Pot",
                ["다이너마이트"] = "Dynamite",
                ["단검"] = "Dagger",
                ["달걀"] = "Egg",
                ["도끼"] = "Axe",
                ["도넛"] = "Donut",
                ["동전"] = "Coin",
                ["뒤집개"] = "Turner",
                ["드라이버"] = "Screwdriver",
                ["드라이어"] = "Hair Dryer",
                ["랜턴"] = "Lantern",
                ["레이저표적지시기"] = "Laser Sight",
                ["렌치"] = "Wrench",
                ["롤리팝"] = "Lollipop",
                ["마녀 모자"] = "Witch Hat",
                ["마법 눈 책"] = "Eye Grimoire",
                ["마법 버섯"] = "Magic Mushroom",
                ["마법봉"] = "Magic Wand",
                ["마법책"] = "Spellbook",
                ["마우스"] = "Mouse",
                ["망치"] = "Hammer",
                ["맥주잔"] = "Beer Mug",
                ["머그컵"] = "Mug",
                ["면도기"] = "Razor",
                ["모니터"] = "Monitor",
                ["모래성 틀"] = "Sandcastle Mold",
                ["묘비"] = "Gravestone",
                ["무덤지기 삽"] = "Gravedigger's Shovel",
                ["물약"] = "Potion",
                ["바"] = "Bar",
                ["바구니"] = "Basket",
                ["바이스"] = "Vise",
                ["방패"] = "Shield",
                ["방향제"] = "Air Freshener",
                ["배구공"] = "Volleyball",
                ["버섯"] = "Mushroom",
                ["베넬리산탄총"] = "Benelli Shotgun",
                ["병"] = "Bottle",
                ["분무기"] = "Spray Bottle",
                ["붕대"] = "Bandage",
                ["비누"] = "Soap",
                ["비치볼"] = "Beach Ball",
                ["빗자루"] = "Broom",
                ["빵"] = "Bread",
                ["뼈"] = "Bone",
                ["뿔피리"] = "Horn",
                ["사과"] = "Apple",
                ["사다리"] = "Ladder",
                ["사탕 그릇"] = "Candy Bowl",
                ["사탕통"] = "Candy Jar",
                ["산탄총"] = "Shotgun",
                ["삽"] = "Shovel",
                ["새우"] = "Shrimp",
                ["샌드위치"] = "Sandwich",
                ["생선"] = "Fish",
                ["서류가방"] = "Briefcase",
                ["서핑보드"] = "Surfboard",
                ["선글라스"] = "Sunglasses",
                ["선인장"] = "Cactus",
                ["선크림"] = "Sunscreen",
                ["섬광탄"] = "Flashbang",
                ["소시지"] = "Sausage",
                ["소총"] = "Rifle",
                ["손"] = "Hand",
                ["손거울"] = "Hand Mirror",
                ["손전등"] = "Flashlight",
                ["손톱깎이"] = "Nail Clippers",
                ["쇠지렛대"] = "Crowbar",
                ["수류탄"] = "Grenade",
                ["수박"] = "Watermelon",
                ["수족관 물고기"] = "Aquarium Fish",
                ["숟가락"] = "Spoon",
                ["슬리퍼"] = "Slippers",
                ["식칼"] = "Kitchen Knife",
                ["십자가"] = "Cross",
                ["십자렌치"] = "Lug Wrench",
                ["쌍안경"] = "Binoculars",
                ["아령"] = "Dumbbell",
                ["아보카도"] = "Avocado",
                ["아이스크림"] = "Ice Cream",
                ["악마의 책"] = "Demon Tome",
                ["야구방망이"] = "Baseball Bat",
                ["약통"] = "Pill Bottle",
                ["양동이"] = "Bucket",
                ["양손검"] = "Greatsword",
                ["양손도끼"] = "Battle Axe",
                ["양초"] = "Candle",
                ["양파"] = "Onion",
                ["연료통"] = "Fuel Can",
                ["연막탄"] = "Smoke Grenade",
                ["열쇠"] = "Key",
                ["오렌지"] = "Orange",
                ["요구르트"] = "Yogurt",
                ["우유팩"] = "Milk Carton",
                ["우지"] = "Uzi",
                ["유리잔"] = "Glass",
                ["의료용가위"] = "Medical Scissors",
                ["의자"] = "Chair",
                ["의학서적"] = "Medical Book",
                ["작업용콘"] = "Traffic Cone",
                ["잘린 손"] = "Severed Hand",
                ["장난감"] = "Toy",
                ["장부"] = "Ledger",
                ["저격소총"] = "Sniper Rifle",
                ["전동드릴"] = "Power Drill",
                ["접시"] = "Plate",
                ["조준경"] = "Scope",
                ["주먹무기"] = "Knuckle Duster",
                ["주방칼"] = "Chef's Knife",
                ["줄자"] = "Tape Measure",
                ["지폐"] = "Banknote",
                ["지폐 다발"] = "Cash Bundle",
                ["지폐 상자"] = "Cash Box",
                ["차"] = "Tea",
                ["차크람"] = "Chakram",
                ["창"] = "Spear",
                ["책"] = "Book",
                ["책상"] = "Desk",
                ["책장"] = "Bookshelf",
                ["촛대"] = "Candlestick",
                ["치즈"] = "Cheese",
                ["치즈케이크"] = "Cheesecake",
                ["카드"] = "Cards",
                ["카메라"] = "Camera",
                ["칵테일"] = "Cocktail",
                ["칼"] = "Knife",
                ["캔"] = "Can",
                ["커피"] = "Coffee",
                ["컵"] = "Cup",
                ["코코넛"] = "Coconut",
                ["코코넛 칵테일"] = "Coconut Cocktail",
                ["쿠키"] = "Cookie",
                ["크루아상"] = "Croissant",
                ["키보드"] = "Keyboard",
                ["타월"] = "Towel",
                ["탁자"] = "Table",
                ["탄산음료"] = "Soda",
                ["텔레비전"] = "Television",
                ["토마토"] = "Tomato",
                ["톱"] = "Saw",
                ["투척칼"] = "Throwing Knife",
                ["튜브"] = "Swim Ring",
                ["티슈"] = "Tissues",
                ["파라솔"] = "Parasol",
                ["페이스트리"] = "Pastry",
                ["포크"] = "Fork",
                ["폭탄"] = "Bomb",
                ["풍선"] = "Balloon",
                ["프라이팬"] = "Frying Pan",
                ["피망"] = "Bell Pepper",
                ["하키스틱"] = "Hockey Stick",
                ["한손검"] = "One-Handed Sword",
                ["한손도끼"] = "Hand Axe",
                ["해골"] = "Skull",
                ["해골 양초"] = "Skull Candle",
                ["햄버거"] = "Burger",
                ["허수아비"] = "Scarecrow",
                ["헤어롤"] = "Hair Roller",
                ["헤어제품"] = "Hair Product",
                ["현금 계수기"] = "Cash Counter",
                ["호박"] = "Pumpkin",
                ["홀"] = "Scepter",
                ["화분"] = "Flower Pot",
                ["화살"] = "Arrow",
                ["화살통"] = "Quiver",
                ["활"] = "Bow",
                ["횃불"] = "Torch",
                ["물건"] = "item",
            };

        /// <summary>
        /// The item's name in <paramref name="language"/>, or the authored
        /// Korean label when that language has no name for it.
        /// </summary>
        public static string Localized(string displayName, string language)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            var trimmed = displayName.Trim();
            if (!string.Equals(language, "en", StringComparison.Ordinal))
            {
                return trimmed;
            }

            return English.TryGetValue(trimmed, out var english) ? english : trimmed;
        }

        /// <summary>The item's name in the language last applied.</summary>
        public static string Localized(string displayName) =>
            Localized(displayName, UiLocale.AppliedLanguage);
    }
}
