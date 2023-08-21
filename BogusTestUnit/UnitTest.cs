using System;
using Bogus;
using Bogus.DataSets;
using Bogus.Extensions;
using Newtonsoft.Json;

namespace TestUnit
{
    class UnitTest
    {
        //количество данных
        private const int MAX_COUNT_USERS = 10;
        private const int MAX_COUNT_INSPECTS = 10;
        private const int MAX_COUNT_DEFECTS = 15;
        private const int MAX_COUNT_MASTERS = 10;
        private const int MAX_COUNT_NATURES = 150;

        //счетчики данных
        private static int count_users = 0;
        private static int count_inspects = 0;
        private static int count_defects = 0;
        private static int count_masters = 0;

        static void Main()
        {
            var generate_poi_defects = CreatePoiDefects ();
            var generate_defects = CreateDefects ( generate_poi_defects );
            //CreateMasterModule ();
            var generate_inspects = CreateInspects ( generate_defects );
            for (int i=0; i < MAX_COUNT_INSPECTS; i++)
            {
                var generate_representations = CreateRepresentations(count_users, count_inspects);
                Dump(generate_representations);
            }
            var generate_users = CreateUsers( generate_inspects );
            var users = generate_users.Generate(MAX_COUNT_USERS);
            Dump(users);
        }

        private static Faker<UserData> CreateUsers(Faker<InspectData> generateInspects)
        {
            Faker<UserData> generate_users = new Faker<UserData>()
                .StrictMode(true)
               //Optional: Call for objects that have complex initialization
               .CustomInstantiator(f => new UserData(count_users++))
                .RuleFor(u => u.login, f => UserName_En(f.Name.LastName()))
                //.RuleFor(u => u.Avatar, f => f.Internet.Avatar())
                .RuleFor(u => u.password, f => f.Internet.Password())
                .RuleFor(u => u.FIO, f => $"{f.Name.LastName()} {f.Lorem.Letter().ToUpper()}.{f.Lorem.Letter().ToUpper()}")
                .RuleFor(u => u.tab_num, f => f.Random.Replace("######"))
                .RuleFor(u => u.guild, f => f.Random.Number(1, 100).ToString())
                .RuleFor(u => u.position, f => f.Name.JobTitle())//"Контрольный мастер"
                .RuleFor(u => u.rule, f => f.Name.JobDescriptor())
                //.RuleFor(u => u.inspects, f => ...
                .RuleFor(u => u.num_login, f => f.Random.Number(1, 25))
                .RuleFor(u => u.source, f => f.Internet.UrlWithPath())

                //Optional: After all rules are applied finish with the following action
                .FinishWith ((f, u) =>
                {
                    Console.WriteLine("User Created! key={0}", u.key);
                });
            return generate_users;
        }

        public static string UserName_En (string LastName)
        {
            var username = new Faker("en");
            return username.Internet.UserName( LastName );
        }

        private static Faker<InspectData> CreateInspects (Faker<DefectData> generateDefects)
        {
            Faker<InspectData> generateModules = new Faker<InspectData>()
                .StrictMode(true)
               //Optional: Call for objects that have complex initialization
               .CustomInstantiator(f => new InspectData(count_inspects++))
                .RuleFor(u => u.guild, f => f.Random.Number(1, 100).ToString())
                .RuleFor(u => u.inspect_object, (f, u) => f.Lorem.Word())
                //TODO: подцепить program
                .RuleFor (u => u.program, f => $"{f.Lorem.Words(3)}")
                .RuleFor(u => u.exemplar, f => f.Random.Replace("######"))
                .RuleFor(u => u.date_module, f => f.Date.RecentOffset().ToString())
                .RuleFor(u => u.work_hour, f => f.Random.Float())
                .RuleFor(u => u.master, f => f.Random.Number ( 1 , MAX_COUNT_MASTERS ) )
                .RuleFor(u => u.description, f => f.Lorem.Text())
                .RuleFor(u => u.completion, f => f.Random.Bool())
                .RuleFor(u => u.status, f => f.PickRandom(InspectData.Status))
                //.RuleFor ( u => u.defectData , f => ...
                //hack: подцепить module_state

                //Optional: After all rules are applied finish with the following action
                .FinishWith((f, u) =>
                {
                    Console.WriteLine("Inspect Created! key={0}", u.key);
                });
            return generateModules;
            //Modules.Dump ();
        }

        private static Faker<DefectData> CreateDefects (Faker<POIDefectData> generateSettingDefects)
        {
            //NatureDefectData [] NatureDefect = new NatureDefectData [NatureDefectNum];
            ////получаем данные из JSON файла
            //NatureDefect = GetNatureDefect ( NatureDefect );

            Faker<DefectData> generateDefects = new Faker<DefectData>()
                .StrictMode(true)
               //Optional: Call for objects that have complex initialization
               .CustomInstantiator(f => new DefectData(count_defects++))
                .RuleFor (u => u.foto1_source, f => f.Image.PicsumUrl() )
                .RuleFor(u => u.foto2_source, f => f.Image.PicsumUrl())
                .RuleFor(u => u.foto3_source, f => f.Image.PicsumUrl())
                .RuleFor (u => u.category, f => f.Random.Int(1, MAX_COUNT_NATURES) )
                .RuleFor (u => u.culprit, f => f.PickRandom (DefectData.Culprit) )
                .RuleFor(u => u.explic , f => f.PickRandom (DefectData.Explicit) )
                .RuleFor(u => u.significant , f => f.PickRandom (DefectData.Significant) )
                .RuleFor(u => u.regular , f => f.PickRandom (DefectData.Regular) )
                .RuleFor(u => u.removable , f => f.PickRandom(DefectData.Removable) )
                .RuleFor(u => u.perfomer , f => f.PickRandom (DefectData.Perfomer) )
                .RuleFor ( u => u.descrpipt , f => f.Lorem.Text ().OrNull(f) )
                .RuleFor ( u => u.date , f => f.Date.Recent ().ToString () )
                .RuleFor ( u => u.defect_repeat , f => f.Random.Bool() )
                .RuleFor ( u => u.status , f => f.PickRandom (DefectData.Status) )
                //hack: подцепить defect_state

                //Optional: After all rules are applied finish with the following action
                .FinishWith((f, u) =>
                {
                    Console.WriteLine( "Defect Created! key={0}" , u.key );
                });
            return generateDefects;
            //Defects.Dump ();
        }

        private static Faker<POIDefectData> CreatePoiDefects ()
        {
            var num_defect = 0;
            Faker<POIDefectData> generateSettingDefects = new Faker<POIDefectData> ()
                .StrictMode(true)
               //Optional: Call for objects that have complex initialization
               .CustomInstantiator(f => new POIDefectData(num_defect++))
                
                .RuleFor ( u => u.xposPOIAncor , f => f.Random.Float (0 , 1) )
                .RuleFor ( u => u.yposPOIAncor , f => f.Random.Float (0 , 1) )
                .RuleFor ( u => u.zposPOIAncor , f => f.Random.Float (0 , 1) )
                .RuleFor ( u => u.rotate_xposToolTip , f => f.Random.Float ( -100 , 100 ) )
                .RuleFor ( u => u.rotate_yposToolTip , f => f.Random.Float ( -100 , 100 ) )
                .RuleFor ( u => u.rotate_zposToolTip , f => f.Random.Float ( -100 , 100 ) )
                .RuleFor ( u => u.xposContent , f => 0F )
                .RuleFor ( u => u.yposContent , f => 0F )
                .RuleFor ( u => u.zposContent , f => 0F )
                .RuleFor ( u => u.rotate_xposContent , f => 0F )
                .RuleFor ( u => u.rotate_yposContent , f => 0F )
                .RuleFor ( u => u.rotate_zposContent , f => 0F )
                .RuleFor ( u => u.size_xposContent , f => 1.2F )
                .RuleFor ( u => u.size_yposContent , f => 1.2F )
                .RuleFor ( u => u.size_zposContent , f => 1.2F )

                //Optional: After all rules are applied finish with the following action
                .FinishWith ( (f , u) =>
                {
                    Console.WriteLine ( "POIDefect Created! key={0}" , u.key );
                } );
            return generateSettingDefects;
            //SettingDefects.Dump ();
        }

        private static RepresentationData CreateRepresentations (int numUser, int numInspect)
        {
            bool [] IsChooseRepresent = new [] { true , false };
            var randomChoose = new Faker ("en");
            RepresentationData generate_represention = new RepresentationData(numUser, numInspect);

            //TODO: подцепить num_user num_inspect
            generate_represention.represent_all = randomChoose.Random.Bool();

            if ( generate_represention.represent_all )
            {
               generate_represention.represent_solved = false;
               generate_represention.represent_taken_solved = false;
               generate_represention.represent_unresolved = false;
            }
            else
            {
               generate_represention.represent_solved = randomChoose.Random.Bool();

               if( generate_represention.represent_solved )
               {
                  generate_represention.represent_taken_solved = false;
                  generate_represention.represent_unresolved = false;
               }
               else
               {
                  generate_represention.represent_taken_solved = randomChoose.Random.Bool();
                  if ( generate_represention.represent_taken_solved ) 
                        generate_represention.represent_unresolved = false;
                  else generate_represention.represent_unresolved = true;
               }
            }

            Console.WriteLine( "Representation Created! num_inspect={0}" , generate_represention.num_inspect );
            return generate_represention;
        }

        //private static void CreateMasterModule ()
        //{
        //    Faker<MasterModuleData> generateMasterModule = new Faker<MasterModuleData> ()
        //        .StrictMode ( true )
        //       //Optional: Call for objects that have complex initialization
        //       .CustomInstantiator ( f => new MasterModuleData(NumMaster++ ) )
        //        .RuleFor ( u => u.FIO , f => $"{f.Name.LastName ()} {f.Lorem.Letter ().ToUpper ()}.{f.Lorem.Letter ().ToUpper ()}" )
        //        .RuleFor ( u => u.source , f => f.Internet.UrlWithPath () )

        //        //Optional: After all rules are applied finish with the following action
        //        .FinishWith ( (f , u) =>
        //        {
        //            Console.WriteLine ( "Master Module Created! num_master={0}" , u.num_master );
        //        } );
        //    var masterModule = generateMasterModule.Generate ( MasterModuleNum );
        //    Dump(masterModule);
        //}
   
        /// <summary>
        /// Получение данных из JSON
        /// </summary>
        /// <param name="NatureDefect"></param>
        /// <returns></returns>
        private static NatureDefectData GetNatureDefect (NatureDefectData [] NatureDefect)
        {
            //TODO: добавить JSON
            //IDictionary<string , object> players = Json.Deserialize ( serviceData ) as IDictionary<string , object>;

            NatureDefectData generateNatureDefects = new NatureDefectData ();
            
            return generateNatureDefects;
        }

        public static void Dump(object obj)
        {
            Console.WriteLine(DumpString(obj));
        }

        public static string DumpString(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

    }
}
