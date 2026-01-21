using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using TestCases.CustomRequestBinder;
using TestCases.FormBindingComplexDtos;
using ByteEnum = TestCases.QueryObjectBindingTest.ByteEnum;
using Endpoint = TestCases.JsonArrayBindingToListOfModels.Endpoint;
using Person = TestCases.RouteBindingTest.Person;
using Request = TestCases.RouteBindingTest.Request;
using Response = TestCases.RouteBindingInEpWithoutReq.Response;

namespace Binding;

public class BindingTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task RouteValueReadingInEndpointWithoutRequest()
    {
        var (rsp, res) = await App.Client.GETAsync<
                             EmptyRequest,
                             Response>("/api/test-cases/ep-witout-req-route-binding-test/09809/12", EmptyRequest.Instance);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.CustomerID.ShouldBe(09809);
        res.OtherID.ShouldBe(12);
    }

    [Fact]
    public async Task RouteValueReadingIsRequired()
    {
        var (rsp, res) = await App.Client.GETAsync<
                             EmptyRequest,
                             ErrorResponse>("/api/test-cases/ep-witout-req-route-binding-test/09809/lkjhlkjh", EmptyRequest.Instance);

        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors.ShouldNotBeNull();
        res.Errors.ShouldContainKey("otherId");
    }

    [Fact]
    public async Task StronglyTypedRouteBinding()
    {
        var (rsp, res) = await App.Client.POSTAsync<TestCases.StronglyTypedRouteParamTest.Request, TestCases.StronglyTypedRouteParamTest.Request>(
                             "api/test-cases/strong-route-params/123/blah/jacky",
                             new()
                             {
                                 Name = "x",
                                 Uid = "y"
                             });

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBeEquivalentTo(
            new TestCases.StronglyTypedRouteParamTest.Request
            {
                Name = "jacky",
                Uid = "123"
            });
    }

    [Fact]
    public async Task RouteValueBinding()
    {
        var (rsp, res) = await App.Client
                                  .POSTAsync<Request, TestCases.RouteBindingTest.Response>(
                                      "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45?Url=https://test.com&Custom=12&CustomList=1;2",
                                      new()
                                      {
                                          Bool = false,
                                          DecimalNumber = 1,
                                          Double = 1,
                                          FromBody = "from body value",
                                          Int = 1,
                                          Long = 1,
                                          String = "nothing",
                                          Custom = new() { Value = 11111 },
                                          CustomList = [0]
                                      });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.String.ShouldBe("something");
        res.Bool.ShouldBe(true);
        res.Int.ShouldBe(99);
        res.Long.ShouldBe(483752874564876);
        res.Double.ShouldBe(2232.12);
        res.FromBody.ShouldBe("from body value");
        res.Decimal.ShouldBe(123.45m);
        res.Url.ShouldBe("https://test.com/");
        res.Custom.Value.ShouldBe(12);
        res.CustomList.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task RouteValueBindingFromQueryParams()
    {
        var (rsp, res) = await App.Client
                                  .POSTAsync<Request, TestCases.RouteBindingTest.Response>(
                                      "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45/" +
                                      "?Bool=false&String=everything",
                                      new()
                                      {
                                          Bool = false,
                                          DecimalNumber = 1,
                                          Double = 1,
                                          FromBody = "from body value",
                                          Int = 1,
                                          Long = 1,
                                          String = "nothing"
                                      });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.String.ShouldBe("everything");
        res.Bool.ShouldBeFalse();
        res.Int.ShouldBe(99);
        res.Long.ShouldBe(483752874564876);
        res.Double.ShouldBe(2232.12);
        res.FromBody.ShouldBe("from body value");
        res.Decimal.ShouldBe(123.45m);
        res.Blank.ShouldBeNull();
    }

    [Fact]
    public async Task JsonArrayBindingToIEnumerableProps()
    {
        var (rsp, res) = await App.Client
                                  .GETAsync<TestCases.JsonArrayBindingForIEnumerableProps.Request,
                                      TestCases.JsonArrayBindingForIEnumerableProps.Response>(
                                      "/api/test-cases/json-array-binding-for-ienumerable-props?" +
                                      "doubles=[123.45,543.21]&" +
                                      "dates=[\"2022-01-01\",\"2022-02-02\"]&" +
                                      "guids=[\"b01ec302-0adc-4a2b-973d-bbfe639ed9a5\",\"e08664a4-efd8-4062-a1e1-6169c6eac2ab\"]&" +
                                      "ints=[1,2,3]&" +
                                      "steven={\"age\":12,\"name\":\"steven\"}&" +
                                      "dict={\"key1\":\"val1\",\"key2\":\"val2\"}",
                                      new());

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Doubles.Length.ShouldBe(2);
        res.Doubles[0].ShouldBe(123.45);
        res.Dates.Count.ShouldBe(2);
        res.Dates.First().ShouldBe(DateTime.Parse("2022-01-01"));
        res.Guids.Count.ShouldBe(2);
        res.Guids[0].ShouldBe(Guid.Parse("b01ec302-0adc-4a2b-973d-bbfe639ed9a5"));
        res.Ints.Count().ShouldBe(3);
        res.Ints.First().ShouldBe(1);
        res.Steven.ShouldBeEquivalentTo(
            new TestCases.JsonArrayBindingForIEnumerableProps.Request.Person
            {
                Age = 12,
                Name = "steven"
            });
        res.Dict.Count.ShouldBe(2);
        res.Dict["key1"].ShouldBe("val1");
        res.Dict["key2"].ShouldBe("val2");
    }

    [Fact]
    public async Task JsonArrayBindingToListOfModels()
    {
        var (rsp, res) = await App.Client.POSTAsync<
                             Endpoint,
                             List<TestCases.JsonArrayBindingToListOfModels.Request>,
                             List<TestCases.JsonArrayBindingToListOfModels.Response>>(
                         [
                             new() { Name = "test1" },
                             new() { Name = "test2" }
                         ]);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Count.ShouldBe(2);
        res[0].Name.ShouldBe("test1");
    }

    [Fact]
    public async Task JsonArrayBindingToIEnumerableDto()
    {
        var req = new TestCases.JsonArrayBindingToIEnumerableDto.Request
        {
            new() { Id = 1, Name = "one" },
            new() { Id = 2, Name = "two" }
        };

        var (rsp, res) = await App.Client.POSTAsync<
                             TestCases.JsonArrayBindingToIEnumerableDto.Endpoint,
                             TestCases.JsonArrayBindingToIEnumerableDto.Request,
                             List<TestCases.JsonArrayBindingToIEnumerableDto.Response>>(req);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Count.ShouldBe(2);
        res.ShouldBeEquivalentTo(req.Select(x => new TestCases.JsonArrayBindingToIEnumerableDto.Response { Id = x.Id, Name = x.Name }).ToList());
    }

    [Fact]
    public async Task DupeParamBindingToIEnumerableProps()
    {
        var (rsp, res) = await App.Client
                                  .GETAsync<TestCases.DupeParamBindingForIEnumerableProps.Request,
                                      TestCases.DupeParamBindingForIEnumerableProps.Response>(
                                      "/api/test-cases/dupe-param-binding-for-ienumerable-props?" +
                                      "doubles=123.45&" +
                                      "doubles=543.21&" +
                                      "dates=2022-01-01&" +
                                      "dates=2022-02-02&" +
                                      "guids=b01ec302-0adc-4a2b-973d-bbfe639ed9a5&" +
                                      "guids=e08664a4-efd8-4062-a1e1-6169c6eac2ab&" +
                                      "ints=1&" +
                                      "ints=2&" +
                                      "ints=3&" +
                                      "strings=[1,2]&" +
                                      "strings=three&" +
                                      "morestrings=[\"one\",\"two\"]&" +
                                      "morestrings=three&" +
                                      "persons={\"name\":\"john\",\"age\":45}&" +
                                      "persons={\"name\":\"doe\",\"age\":55}",
                                      new());

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Doubles.Length.ShouldBe(2);
        res.Doubles[0].ShouldBe(123.45);
        res.Dates.Count.ShouldBe(2);
        res.Dates.First().ShouldBe(DateTime.Parse("2022-01-01"));
        res.Guids.Count.ShouldBe(2);
        res.Guids[0].ShouldBe(Guid.Parse("b01ec302-0adc-4a2b-973d-bbfe639ed9a5"));
        res.Ints.Count().ShouldBe(3);
        res.Ints.First().ShouldBe(1);
        res.Strings.Length.ShouldBe(2);
        res.Strings[0].ShouldBe("[1,2]");
        res.MoreStrings.Length.ShouldBe(2);
        res.MoreStrings[0].ShouldBe("[\"one\",\"two\"]");
        res.Persons.Count().ShouldBe(2);
        res.Persons.First().Name.ShouldBe("john");
        res.Persons.First().Age.ShouldBe(45);
        res.Persons.Last().Name.ShouldBe("doe");
        res.Persons.Last().Age.ShouldBe(55);
    }

    [Fact]
    public async Task BindingFromAttributeUse()
    {
        var (rsp, res) = await App.Client
                                  .POSTAsync<Request, TestCases.RouteBindingTest.Response>(
                                      "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45/" +
                                      "?Bool=false&String=everything&XBlank=256" +
                                      "&age=45&name=john&id=10c225a6-9195-4596-92f5-c1234cee4de7" +
                                      "&numbers=0&numbers=1&numbers=-222&numbers=1000&numbers=22" +
                                      "&child.id=8bedccb3-ff93-47a2-9fc4-b558cae41a06" +
                                      "&child.name=child name&child.age=-22" +
                                      "&child.strings[0]=string1&child.strings[1]=string2&child.strings[2]=&child.strings[3]=strangeString",
                                      new()
                                      {
                                          Bool = false,
                                          DecimalNumber = 1,
                                          Double = 1,
                                          FromBody = "from body value",
                                          Int = 1,
                                          Long = 1,
                                          String = "nothing",
                                          Blank = 1,
                                          Person = new()
                                          {
                                              Age = 50,
                                              Name = "wrong"
                                          }
                                      });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.String.ShouldBe("everything");
        res.Bool.ShouldBeFalse();
        res.Int.ShouldBe(99);
        res.Long.ShouldBe(483752874564876);
        res.Double.ShouldBe(2232.12);
        res.FromBody.ShouldBe("from body value");
        res.Decimal.ShouldBe(123.45m);
        res.Blank.ShouldBe(256);
        res.Person.ShouldNotBeNull();
        res.Person.ShouldBeEquivalentTo(
            new Person
            {
                Age = 45,
                Name = "john",
                Id = Guid.Parse("10c225a6-9195-4596-92f5-c1234cee4de7"),
                Child = new()
                {
                    Age = -22,
                    Name = "child name",
                    Id = Guid.Parse("8bedccb3-ff93-47a2-9fc4-b558cae41a06"),
                    Strings = ["string1", "string2", "", "strangeString"]
                },
                Numbers = [0, 1, -222, 1000, 22]
            });
    }

    [Fact]
    public async Task BindingObjectFromQueryUse()
    {
        var (rsp, res) = await App.Client
                                  .GETAsync<TestCases.QueryObjectBindingTest.Request, TestCases.QueryObjectBindingTest.Response>(
                                      "api/test-cases/query-object-binding-test" +
                                      "?BoOl=TRUE&String=everything&iNt=99&long=483752874564876&DOUBLE=2232.12&Enum=3" +
                                      "&age=45&name=john&id=10c225a6-9195-4596-92f5-c1234cee4de7" +
                                      "&numbers=0&numbers=1&numbers=-222&numbers=1000&numbers=22" +
                                      "&favoriteDay=Friday&IsHidden=FALSE&ByteEnum=2" +
                                      "&child.id=8bedccb3-ff93-47a2-9fc4-b558cae41a06" +
                                      "&child.name=child name&child.age=-22" +
                                      "&CHILD.FavoriteDays=1&ChiLD.FavoriteDays=Saturday&CHILD.ISHiddeN=TruE" +
                                      "&child.strings=string1&child.strings=string2&child.strings=&child.strings=strangeString",
                                      new());

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.ShouldBeEquivalentTo(
            new TestCases.QueryObjectBindingTest.Response
            {
                Double = 2232.12,
                Bool = true,
                Enum = DayOfWeek.Wednesday,
                String = "everything",
                Int = 99,
                Long = 483752874564876,
                Person = new()
                {
                    Age = 45,
                    Name = "john",
                    Id = Guid.Parse("10c225a6-9195-4596-92f5-c1234cee4de7"),
                    FavoriteDay = DayOfWeek.Friday,
                    ByteEnum = ByteEnum.AnotherCheck,
                    IsHidden = false,
                    Child = new()
                    {
                        Age = -22,
                        Name = "child name",
                        Id = Guid.Parse("8bedccb3-ff93-47a2-9fc4-b558cae41a06"),
                        Strings = ["string1", "string2", "", "strangeString"],
                        FavoriteDays = [DayOfWeek.Monday, DayOfWeek.Saturday],
                        IsHidden = true
                    },
                    Numbers = [0, 1, -222, 1000, 22]
                }
            });
    }

    [Fact]
    public async Task BindingArraysOfObjectsFromQueryUse()
    {
        var (rsp, res) = await App.Client
                                  .GETAsync<TestCases.QueryObjectWithObjectsArrayBindingTest.Request,
                                      TestCases.QueryObjectWithObjectsArrayBindingTest.Response>(
                                      "api/test-cases/query-arrays-of-objects-binding-test" +
                                      "?Child.Objects[0].String=test&Child.Objects[0].Bool=true&Child.Objects[0].Double=22.22&Child.Objects[0].Enum=4" +
                                      "&Child.Objects[0].Int=31&Child.Objects[0].Long=22" +
                                      "&Child.Objects[1].String=test2&Child.Objects[1].Enum=Wednesday" +
                                      "&Objects[0].String=test&Objects[0].Bool=true&Objects[0].Double=22.22&Objects[0].Enum=4" +
                                      "&Objects[0].Int=31&Objects[0].Long=22" +
                                      "&Objects[1].String=test2&Objects[1].Enum=Wednesday",
                                      new());

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);

        res.ShouldBeEquivalentTo(
            new TestCases.QueryObjectWithObjectsArrayBindingTest.Response
            {
                Person = new()
                {
                    Child = new()
                    {
                        Objects =
                        [
                            new()
                            {
                                String = "test",
                                Bool = true,
                                Double = 22.22,
                                Enum = DayOfWeek.Thursday,
                                Int = 31,
                                Long = 22
                            },

                            new()
                            {
                                String = "test2",
                                Enum = DayOfWeek.Wednesday
                            }
                        ]
                    },
                    Objects =
                    [
                        new()
                        {
                            String = "test",
                            Bool = true,
                            Double = 22.22,
                            Enum = DayOfWeek.Thursday,
                            Int = 31,
                            Long = 22
                        },

                        new()
                        {
                            String = "test2",
                            Enum = DayOfWeek.Wednesday
                        }
                    ]
                }
            });
    }

    [Fact]
    public async Task RangeHandling()
    {
        var res = await App.RangeClient.GetStringAsync("api/test-cases/range", Cancellation);
        res.ShouldBe("fghij");
    }

    [Fact]
    public async Task FileHandling()
    {
        using var imageContent = new ByteArrayContent(
            await new StreamContent(File.OpenRead("test.png"))
                .ReadAsByteArrayAsync(Cancellation));
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        using var form = new MultipartFormDataContent
        {
            { imageContent, "File", "test.png" },
            { new StringContent("500"), "Width" },
            { new StringContent("500"), "Height" }
        };

        var res = await App.AdminClient.PostAsync("api/uploads/image/save", form, Cancellation);

        using var md5Instance = MD5.Create();
        await using var stream = await res.Content.ReadAsStreamAsync(Cancellation);
        var resMD5 = BitConverter.ToString(md5Instance.ComputeHash(stream)).Replace("-", "");

        resMD5.ShouldBe("8A1F6A8E27D2E440280050DA549CBE3E");
    }

    [Fact]
    public async Task FileHandlingFileBinding()
    {
        for (var i = 0; i < 3; i++) //repeat upload multiple times
        {
            await using var stream1 = File.OpenRead("test.png");
            await using var stream2 = File.OpenRead("test.png");
            await using var stream3 = File.OpenRead("test.png");

            var req = new Uploads.Image.SaveTyped.Request
            {
                File1 = new FormFile(stream1, 0, stream1.Length, "File1", "test.png")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "image/png"
                },
                File2 = new FormFile(stream2, 0, stream2.Length, "File2", "test.png"),
                File3 = new FormFile(stream3, 0, stream2.Length, "File3", "test.png"),
                Width = 500,
                Height = 500,
                GuidId = Guid.NewGuid()
            };

            var res = await App.AdminClient.POSTAsync<
                          Uploads.Image.SaveTyped.Endpoint,
                          Uploads.Image.SaveTyped.Request>(req, sendAsFormData: true);

            using var md5Instance = MD5.Create();
            await using var stream = await res.Content.ReadAsStreamAsync(Cancellation);
            var resMd5 = BitConverter.ToString(md5Instance.ComputeHash(stream)).Replace("-", "");

            resMd5.ShouldBe("8A1F6A8E27D2E440280050DA549CBE3E");
        }
    }

    [Fact]
    public async Task FormFileCollectionBinding()
    {
        await using var stream1 = File.OpenRead("test.png");
        await using var stream2 = File.OpenRead("test.png");
        await using var stream3 = File.OpenRead("test.png");
        await using var stream4 = File.OpenRead("test.png");
        await using var stream5 = File.OpenRead("test.png");
        await using var stream6 = File.OpenRead("test.png");

        var req = new TestCases.FormFileBindingTest.Request
        {
            File1 = new FormFile(stream1, 0, stream1.Length, "file1", "test1.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            },
            File2 = new FormFile(stream2, 0, stream2.Length, "file2", "test2.png"),

            Cars = new FormFileCollection
            {
                new FormFile(stream3, 0, stream3.Length, "car1", "car1.png"),
                new FormFile(stream4, 0, stream4.Length, "car2", "car2.png")
            },

            Jets = new FormFileCollection
            {
                new FormFile(stream5, 0, stream5.Length, "jet1", "jet1.png"),
                new FormFile(stream6, 0, stream6.Length, "jet2", "jet2.png")
            },

            Width = 500,
            Height = 500
        };

        var (rsp, res) = await App
                               .AdminClient
                               .POSTAsync<
                                   TestCases.FormFileBindingTest.Endpoint,
                                   TestCases.FormFileBindingTest.Request,
                                   TestCases.FormFileBindingTest.Response>(req, sendAsFormData: true);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.File1Name.ShouldBe("test1.png");
        res.File2Name.ShouldBe("test2.png");
        res.CarNames.ShouldBe(["car1.png", "car2.png"]);
        res.JetNames.ShouldBe(["jet1.png", "jet2.png"]);
    }

    [Fact]
    public async Task ComplexFormDataBindingViaSendAsFormData()
    {
        var book = new Book
        {
            BarCodes = new List<int>([1, 2, 3]),
            CoAuthors = [new() { Name = "a1" }, new() { Name = "a2" }],
            MainAuthor = new() { Name = "main" }
        };

        var (rsp, res) = await App.GuestClient.PUTAsync<ToFormEndpoint, Book, Book>(book, sendAsFormData: true);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBeEquivalentTo(book);
    }

    [Fact]
    public async Task ComplexFormDataBinding()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/test-cases/form-binding-complex-dtos");
        var content = new MultipartFormDataContent();

        content.Add(new StringContent("book title"), "Title");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoverImage", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "SourceFiles[1]", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "SourceFiles[0]", "test.png");
        content.Add(new StringContent("main author name"), "MainAuthor.Name");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.ProfileImage", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.DocumentFiles", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.DocumentFiles", "test.png");
        content.Add(new StringContent("main author address street"), "MainAuthor.MainAddress.Street");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.MainAddress.MainImage", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.MainAddress.AlternativeImages", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.MainAddress.AlternativeImages", "test.png");
        content.Add(new StringContent("main author other address 0 street"), "MainAuthor.OtherAddresses[0].Street");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.OtherAddresses[0].MainImage", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.OtherAddresses[0].AlternativeImages", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.OtherAddresses[0].AlternativeImages", "test.png");
        content.Add(new StringContent("main author other address 1 street"), "MainAuthor.OtherAddresses[1].Street");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.OtherAddresses[1].MainImage", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.OtherAddresses[1].AlternativeImages", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "MainAuthor.OtherAddresses[1].AlternativeImages", "test.png");
        content.Add(new StringContent("co author 0 name"), "CoAuthors[0].Name");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].ProfileImage", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].DocumentFiles", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].DocumentFiles", "test.png");
        content.Add(new StringContent("co author 0 address street"), "CoAuthors[0].MainAddress.Street");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].MainAddress.MainImage", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].MainAddress.AlternativeImages", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].MainAddress.AlternativeImages", "test.png");
        content.Add(new StringContent("co author 0 other address 0 street"), "CoAuthors[0].OtherAddresses[0].Street");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].OtherAddresses[0].MainImage", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].OtherAddresses[0].AlternativeImages[1]", "test.png");
        content.Add(new StreamContent(File.OpenRead("test.png")), "CoAuthors[0].OtherAddresses[0].AlternativeImages[0]", "test.png");
        content.Add(new StringContent("12345"), "BarCodes");
        content.Add(new StringContent("54321"), "BarCodes");
        request.Content = content;
        var response = await App.GuestClient.SendAsync(request, Cancellation);
        response.EnsureSuccessStatusCode();

        var x = TestCases.FormBindingComplexDtos.Endpoint.Result;
        x.ShouldNotBeNull();

        x!.Title.ShouldBe("book title");
        x.CoverImage.Name.ShouldBe("CoverImage");
        x.SourceFiles.Count.ShouldBe(2);
        x.SourceFiles[0].Name.ShouldBe("SourceFiles[0]");
        x.SourceFiles[1].Name.ShouldBe("SourceFiles[1]");
        x.MainAuthor.ShouldNotBeNull();
        x.MainAuthor.Name.ShouldBe("main author name");
        x.MainAuthor.ProfileImage.Name.ShouldBe("MainAuthor.ProfileImage");
        x.MainAuthor.DocumentFiles.Count.ShouldBe(2);
        x.MainAuthor.DocumentFiles[0].Name.ShouldBe("MainAuthor.DocumentFiles");
        x.MainAuthor.DocumentFiles[1].Name.ShouldBe("MainAuthor.DocumentFiles");
        x.MainAuthor.MainAddress.ShouldNotBeNull();
        x.MainAuthor.MainAddress.Street.ShouldBe("main author address street");
        x.MainAuthor.MainAddress.MainImage.Name.ShouldBe("MainAuthor.MainAddress.MainImage");
        x.MainAuthor.MainAddress.AlternativeImages.Count.ShouldBe(2);
        x.MainAuthor.MainAddress.AlternativeImages[0].Name.ShouldBe("MainAuthor.MainAddress.AlternativeImages");
        x.MainAuthor.MainAddress.AlternativeImages[1].Name.ShouldBe("MainAuthor.MainAddress.AlternativeImages");
        x.MainAuthor.OtherAddresses.Count.ShouldBe(2);
        x.MainAuthor.OtherAddresses[0].MainImage.Name.ShouldBe("MainAuthor.OtherAddresses[0].MainImage");
        x.MainAuthor.OtherAddresses[0].AlternativeImages.Count.ShouldBe(2);
        x.MainAuthor.OtherAddresses[0].AlternativeImages[0].Name.ShouldBe("MainAuthor.OtherAddresses[0].AlternativeImages");
        x.MainAuthor.OtherAddresses[0].AlternativeImages[1].Name.ShouldBe("MainAuthor.OtherAddresses[0].AlternativeImages");
        x.MainAuthor.OtherAddresses[1].MainImage.Name.ShouldBe("MainAuthor.OtherAddresses[1].MainImage");
        x.MainAuthor.OtherAddresses[1].AlternativeImages.Count.ShouldBe(2);
        x.MainAuthor.OtherAddresses[1].AlternativeImages[0].Name.ShouldBe("MainAuthor.OtherAddresses[1].AlternativeImages");
        x.MainAuthor.OtherAddresses[1].AlternativeImages[1].Name.ShouldBe("MainAuthor.OtherAddresses[1].AlternativeImages");
        x.CoAuthors.Count.ShouldBe(1);
        x.CoAuthors[0].Name.ShouldBe("co author 0 name");
        x.CoAuthors[0].ProfileImage.Name.ShouldBe("CoAuthors[0].ProfileImage");
        x.CoAuthors[0].DocumentFiles.Count.ShouldBe(2);
        x.CoAuthors[0].DocumentFiles[0].Name.ShouldBe("CoAuthors[0].DocumentFiles");
        x.CoAuthors[0].DocumentFiles[1].Name.ShouldBe("CoAuthors[0].DocumentFiles");
        x.CoAuthors[0].MainAddress.Street.ShouldBe("co author 0 address street");
        x.CoAuthors[0].MainAddress.MainImage.Name.ShouldBe("CoAuthors[0].MainAddress.MainImage");
        x.CoAuthors[0].MainAddress.AlternativeImages.Count.ShouldBe(2);
        x.CoAuthors[0].MainAddress.AlternativeImages[0].Name.ShouldBe("CoAuthors[0].MainAddress.AlternativeImages");
        x.CoAuthors[0].MainAddress.AlternativeImages[1].Name.ShouldBe("CoAuthors[0].MainAddress.AlternativeImages");
        x.CoAuthors[0].OtherAddresses.Count.ShouldBe(1);
        x.CoAuthors[0].OtherAddresses[0].Street.ShouldBe("co author 0 other address 0 street");
        x.CoAuthors[0].OtherAddresses[0].MainImage.Name.ShouldBe("CoAuthors[0].OtherAddresses[0].MainImage");
        x.CoAuthors[0].OtherAddresses[0].AlternativeImages.Count.ShouldBe(2);
        x.CoAuthors[0].OtherAddresses[0].AlternativeImages[0].Name.ShouldBe("CoAuthors[0].OtherAddresses[0].AlternativeImages[0]");
        x.CoAuthors[0].OtherAddresses[0].AlternativeImages[1].Name.ShouldBe("CoAuthors[0].OtherAddresses[0].AlternativeImages[1]");
        x.BarCodes.First().ShouldBe(12345);
        x.BarCodes.Last().ShouldBe(54321);
    }

    [Fact]
    public async Task PlainTextBodyModelBinding()
    {
        using var stringContent = new StringContent("this is the body content");
        stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var rsp = await App.AdminClient.PostAsync("test-cases/plaintext/12345", stringContent, Cancellation);

        var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>(Cancellation);

        res!.BodyContent.ShouldBe("this is the body content");
        res.Id.ShouldBe(12345);
    }

    [Fact]
    public async Task GetRequestWithRouteParameterAndReqDto()
    {
        var (rsp, res) = await App.CustomerClient.GETAsync<EmptyRequest, ErrorResponse>(
                             "/api/sales/orders/retrieve/54321",
                             EmptyRequest.Instance);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Message.ShouldBe("ok!");
    }

    [Fact]
    public async Task QueryParamReadingInEndpointWithoutRequest()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<
                             EmptyRequest,
                             TestCases.QueryParamBindingInEpWithoutReq.Response>(
                             "/api/test-cases/ep-witout-req-query-param-binding-test" +
                             "?customerId=09809" +
                             "&otherId=12" +
                             "&doubles=[123.45,543.21]" +
                             "&guids=[\"b01ec302-0adc-4a2b-973d-bbfe639ed9a5\",\"e08664a4-efd8-4062-a1e1-6169c6eac2ab\"]" +
                             "&ints=[1,2,3]" +
                             "&floaty=3.2",
                             EmptyRequest.Instance);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.CustomerID.ShouldBe(09809);
        res.OtherID.ShouldBe(12);
        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Doubles.Length.ShouldBe(2);
        res.Doubles[0].ShouldBe(123.45);
        res.Guids.Count.ShouldBe(2);
        res.Guids[0].ShouldBe(Guid.Parse("b01ec302-0adc-4a2b-973d-bbfe639ed9a5"));
        res.Ints.Count().ShouldBe(3);
        res.Ints.First().ShouldBe(1);
    }

    [Fact]
    public async Task QueryParamReadingIsRequired()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<
                             EmptyRequest,
                             ErrorResponse>("/api/test-cases/ep-witout-req-query-param-binding-test?customerId=09809&otherId=lkjhlkjh", EmptyRequest.Instance);

        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors.ShouldContainKey("otherId");
    }

    [Fact]
    public async Task FromBodyJsonBinding()
    {
        var (rsp, res) = await App.CustomerClient.POSTAsync<
                             TestCases.FromBodyJsonBinding.Endpoint,
                             TestCases.FromBodyJsonBinding.Request,
                             TestCases.FromBodyJsonBinding.Response>(
                             new()
                             {
                                 Product = new()
                                 {
                                     Id = 202,
                                     Name = "test product",
                                     Price = 200.10m
                                 }
                             },
                             populateHeaders: false);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Product.Name.ShouldBe("test product");
        res.Product.Price.ShouldBe(200.10m);
        res.Product.Id.ShouldBe(202);
        res.CustomerID.ShouldBe(123);
        res.Id.ShouldBe(0);
    }

    [Fact]
    public async Task FromBodyJsonBindingValidationError()
    {
        var (rsp, res) = await App.CustomerClient.POSTAsync<
                             TestCases.FromBodyJsonBinding.Endpoint,
                             TestCases.FromBodyJsonBinding.Request,
                             ErrorResponse>(
                             new()
                             {
                                 Product = new()
                                 {
                                     Id = 202,
                                     Name = "test product",
                                     Price = 10.10m
                                 }
                             });

        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors.Count.ShouldBe(1);
        res.Errors.ContainsKey("product.Price").ShouldBeTrue();
    }

    [Fact]
    public async Task CustomRequestBinder()
    {
        var (rsp, res) = await App.CustomerClient.POSTAsync<
                             TestCases.CustomRequestBinder.Endpoint,
                             Product,
                             TestCases.CustomRequestBinder.Response>(
                             new()
                             {
                                 Id = 202,
                                 Name = "test product",
                                 Price = 10.10m
                             });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Product!.Name.ShouldBe("test product");
        res.Product.Price.ShouldBe(10.10m);
        res.Product.Id.ShouldBe(202);
        res.CustomerID.ShouldBe("123");
        res.Id.ShouldBe(null);
    }

    [Fact]
    public async Task TypedHeaderPropertyBinding()
    {
        using var stringContent = new StringContent("this is the body content");
        stringContent.Headers.ContentDisposition = ContentDispositionHeaderValue.Parse("attachment; filename=\"_filename_.jpg\"");

        var rsp = await App.GuestClient.PostAsync("api/test-cases/typed-header-binding-test", stringContent, Cancellation);
        rsp.IsSuccessStatusCode.ShouldBeTrue();

        var res = await rsp.Content.ReadFromJsonAsync<string>(Cancellation);
        res.ShouldBe("_filename_.jpg");
    }

    [Fact]
    public async Task DontBindAttribute()
    {
        var req = new TestCases.DontBindAttributeTest.Request
        {
            Id = 123,
            Name = "test"
        };

        var rsp = await App.GuestClient.PostAsJsonAsync("api/test-cases/dont-bind-attribute-test/IGNORE_ME", req, Cancellation);
        rsp.IsSuccessStatusCode.ShouldBeTrue();

        var res = await rsp.Content.ReadAsStringAsync(Cancellation);
        res.ShouldBe("123 - test");
    }

    [Fact]
    public async Task FromCookieAttributeBindingPass()
    {
        var id = Guid.NewGuid();
        var ck = Guid.NewGuid().ToString();
        var req = new TestCases.FromCookieRequestBindingTest.Request
        {
            Id = id,
            SomeCookie = ck
        };

        var (rsp, res) = await App.GuestClient.POSTAsync<
                             TestCases.FromCookieRequestBindingTest.Endpoint,
                             TestCases.FromCookieRequestBindingTest.Request,
                             TestCases.FromCookieRequestBindingTest.Response>(req);
        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Id.ShouldBe(id);
        res.CookieValue.ShouldBe(ck);
    }

    [Fact]
    public async Task FromCookieAttributeBindingFail()
    {
        var id = Guid.NewGuid();
        var ck = Guid.NewGuid().ToString();
        var req = new TestCases.FromCookieRequestBindingTest.Request
        {
            Id = id,
            SomeCookie = ck
        };

        var (rsp, res) = await App.GuestClient.POSTAsync<
                             TestCases.FromCookieRequestBindingTest.Endpoint,
                             TestCases.FromCookieRequestBindingTest.Request,
                             ErrorResponse>(req, populateCookies: false);
        rsp.IsSuccessStatusCode.ShouldBeFalse();
        res.Errors.Count.ShouldBe(1);
        res.Errors.Keys.Contains(nameof(TestCases.FromCookieRequestBindingTest.Request.SomeCookie));
    }
}