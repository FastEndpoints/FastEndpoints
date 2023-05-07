using IntegrationTests.Shared.Fixtures;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using TestCases.EventHandlingTest;
using TestCases.RouteBindingTest;
using Xunit;
using Xunit.Abstractions;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace FastEndpoints.IntegrationTests.WebTests;

public class MiscTestCases : EndToEndTestBase
{
    public MiscTestCases(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(
        endToEndTestFixture, outputHelper)
    {
    }

    [Fact]
    public async Task MultiVerbEndpointAnonymousUserPutFail()
    {
        using var imageContent = new ByteArrayContent(Array.Empty<byte>());
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

        using var form = new MultipartFormDataContent { { imageContent, "File", "test.png" } };

        var res = await GuestClient.PutAsync("/api/uploads/image/save", form);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClaimMissing()
    {
        var (_, result) = await AdminClient.POSTAsync<
            TestCases.MissingClaimTest.ThrowIfMissingEndpoint,
            TestCases.MissingClaimTest.ThrowIfMissingRequest,
            ErrorResponse>(new()
            {
                TestProp = "xyz"
            });

        result?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result?.Errors.Should().NotBeNull();
        result?.Errors.Count.Should().Be(1);
        result?.Errors.Should().ContainKey("null-claim");
    }

    [Fact]
    public async Task ClaimMissingButDontThrow()
    {
        var (res, result) = await AdminClient.POSTAsync<
            TestCases.MissingClaimTest.DontThrowIfMissingEndpoint,
            TestCases.MissingClaimTest.DontThrowIfMissingRequest,
            string>(new()
            {
                TestProp = "xyz"
            });

        res?.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Be("you sent xyz");
    }

    [Fact]
    public async Task EmptyRequest()
    {
        var endpointUrl = IEndpoint.TestURLFor<TestCases.EmptyRequestTest.EmptyRequestEndpoint>();

        var requestUri = new Uri(
            AdminClient.BaseAddress!.ToString().TrimEnd('/') +
            (endpointUrl.StartsWith('/') ? endpointUrl : "/" + endpointUrl)
        );

        var message = new HttpRequestMessage
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
            Method = HttpMethod.Get,
            RequestUri = requestUri
        };

        var response = await AdminClient.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HeaderMissing()
    {
        var (_, result) = await AdminClient.POSTAsync<
            TestCases.MissingHeaderTest.ThrowIfMissingEndpoint,
            TestCases.MissingHeaderTest.ThrowIfMissingRequest,
            ErrorResponse>(new()
            {
                TenantID = "abc"
            });

        result?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result?.Errors.Should().NotBeNull();
        result?.Errors.Count.Should().Be(1);
        result?.Errors.Should().ContainKey("TenantID");
    }

    [Fact]
    public async Task HeaderMissingButDontThrow()
    {
        var (res, result) = await AdminClient.POSTAsync<
            TestCases.MissingHeaderTest.DontThrowIfMissingEndpoint,
            TestCases.MissingHeaderTest.DontThrowIfMissingRequest,
            string>(new()
            {
                TenantID = "abc"
            });

        res?.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Be("you sent abc");
    }

    [Fact]
    public async Task RouteValueReadingInEndpointWithoutRequest()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            EmptyRequest,
            TestCases.RouteBindingInEpWithoutReq.Response>(
            "/api/test-cases/ep-witout-req-route-binding-test/09809/12", new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res!.CustomerID.Should().Be(09809);
        res!.OtherID.Should().Be(12);
    }

    [Fact]
    public async Task RouteValueReadingIsRequired()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            EmptyRequest,
            ErrorResponse>(
            "/api/test-cases/ep-witout-req-route-binding-test/09809/lkjhlkjh", new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res?.Errors.Should().NotBeNull();
        res?.Errors.Should().ContainKey("OtherID");
    }

    [Fact]
    public async Task RouteValueBinding()
    {
        var (rsp, res) = await GuestClient
            .POSTAsync<Request, Response>(
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
                    CustomList = new() { 0 }
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.String.Should().Be("something");
        res?.Bool.Should().Be(true);
        res?.Int.Should().Be(99);
        res?.Long.Should().Be(483752874564876);
        res?.Double.Should().Be(2232.12);
        res?.FromBody.Should().Be("from body value");
        res?.Decimal.Should().Be(123.45m);
        res?.Url.Should().Be("https://test.com/");
        res?.Custom.Value.Should().Be(12);
        res?.CustomList.Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task RouteValueBindingFromQueryParams()
    {
        var (rsp, res) = await GuestClient
            .POSTAsync<Request, Response>(
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

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.String.Should().Be("everything");
        res?.Bool.Should().BeFalse();
        res?.Int.Should().Be(99);
        res?.Long.Should().Be(483752874564876);
        res?.Double.Should().Be(2232.12);
        res?.FromBody.Should().Be("from body value");
        res?.Decimal.Should().Be(123.45m);
        res?.Blank.Should().BeNull();
    }

    [Fact]
    public async Task JsonArrayBindingToIEnumerableProps()
    {
        var (rsp, res) = await GuestClient
            .GETAsync<TestCases.JsonArrayBindingForIEnumerableProps.Request, TestCases.JsonArrayBindingForIEnumerableProps.Response>(
            "/api/test-cases/json-array-binding-for-ienumerable-props?" +
            "doubles=[123.45,543.21]&" +
            "dates=[\"2022-01-01\",\"2022-02-02\"]&" +
            "guids=[\"b01ec302-0adc-4a2b-973d-bbfe639ed9a5\",\"e08664a4-efd8-4062-a1e1-6169c6eac2ab\"]&" +
            "ints=[1,2,3]&" +
            "steven={\"age\":12,\"name\":\"steven\"}&" +
            "dict={\"key1\":\"val1\",\"key2\":\"val2\"}",
            new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Doubles.Length.Should().Be(2);
        res?.Doubles[0].Should().Be(123.45);
        res?.Dates.Count.Should().Be(2);
        res?.Dates.First().Should().Be(DateTime.Parse("2022-01-01"));
        res?.Guids.Count.Should().Be(2);
        res?.Guids[0].Should().Be(Guid.Parse("b01ec302-0adc-4a2b-973d-bbfe639ed9a5"));
        res?.Ints.Count().Should().Be(3);
        res?.Ints.First().Should().Be(1);
        res?.Steven.Should().BeEquivalentTo(
            new TestCases.JsonArrayBindingForIEnumerableProps.Request.Person
            {
                Age = 12,
                Name = "steven"
            });
        res?.Dict.Count.Should().Be(2);
        res?.Dict["key1"].Should().Be("val1");
        res?.Dict["key2"].Should().Be("val2");
    }

    [Fact]
    public async Task JsonArrayBindingToListOfModels()
    {
        var (rsp, res) = await GuestClient.POSTAsync<
            TestCases.JsonArrayBindingToListOfModels.Endpoint,
            List<TestCases.JsonArrayBindingToListOfModels.Request>,
            List<TestCases.JsonArrayBindingToListOfModels.Response>>(new()
            {
                { new TestCases.JsonArrayBindingToListOfModels.Request() { Name = "test1" } },
                { new TestCases.JsonArrayBindingToListOfModels.Request() { Name = "test2" } },
            });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Count.Should().Be(2);
        res?[0].Name.Should().Be("test1");
    }

    [Fact]
    public async Task JsonArrayBindingToIEnumerableDto()
    {
        var req = new TestCases.JsonArrayBindingToIEnumerableDto.Request()
        {
            { new TestCases.JsonArrayBindingToIEnumerableDto.Item() { Id = 1,  Name = "one" } },
            { new TestCases.JsonArrayBindingToIEnumerableDto.Item() { Id = 2, Name = "two" } },
        };

        var (rsp, res) = await GuestClient.POSTAsync<
            TestCases.JsonArrayBindingToIEnumerableDto.Endpoint,
            TestCases.JsonArrayBindingToIEnumerableDto.Request,
            List<TestCases.JsonArrayBindingToIEnumerableDto.Response>>(req);

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Count.Should().Be(2);
        res.Should().BeEquivalentTo(req);
    }

    [Fact]
    public async Task DupeParamBindingToIEnumerableProps()
    {
        var (rsp, res) = await GuestClient
            .GETAsync<TestCases.DupeParamBindingForIEnumerableProps.Request, TestCases.DupeParamBindingForIEnumerableProps.Response>(
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

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Doubles.Length.Should().Be(2);
        res?.Doubles[0].Should().Be(123.45);
        res?.Dates.Count.Should().Be(2);
        res?.Dates.First().Should().Be(DateTime.Parse("2022-01-01"));
        res?.Guids.Count.Should().Be(2);
        res?.Guids[0].Should().Be(Guid.Parse("b01ec302-0adc-4a2b-973d-bbfe639ed9a5"));
        res?.Ints.Count().Should().Be(3);
        res?.Ints.First().Should().Be(1);
        res?.Strings.Length.Should().Be(2);
        res?.Strings[0].Should().Be("[1,2]");
        res?.MoreStrings.Length.Should().Be(2);
        res?.MoreStrings[0].Should().Be("[\"one\",\"two\"]");
        res?.Persons.Count().Should().Be(2);
        res?.Persons.First().Name.Should().Be("john");
        res?.Persons.First().Age.Should().Be(45);
        res?.Persons.Last().Name.Should().Be("doe");
        res?.Persons.Last().Age.Should().Be(55);
    }

    [Fact]
    public async Task BindingFromAttributeUse()
    {
        var (rsp, res) = await GuestClient
            .POSTAsync<Request, Response>(
                "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45/" +
                "?Bool=false&String=everything&XBlank=256" +
                "&age=45&name=john&id=10c225a6-9195-4596-92f5-c1234cee4de7" +
                "&numbers[0]=0&numbers[1]=1&numbers[2]=-222&numbers[3]=1000&numbers[4]=22" +
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
                        Name = "wrong",
                    }
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.String.Should().Be("everything");
        res?.Bool.Should().BeFalse();
        res?.Int.Should().Be(99);
        res?.Long.Should().Be(483752874564876);
        res?.Double.Should().Be(2232.12);
        res?.FromBody.Should().Be("from body value");
        res?.Decimal.Should().Be(123.45m);
        res?.Blank.Should().Be(256);
        res?.Person.Should().NotBeNull();
        res?.Person.Should().BeEquivalentTo(new Person
        {
            Age = 45,
            Name = "john",
            Id = Guid.Parse("10c225a6-9195-4596-92f5-c1234cee4de7"),
            Child = new()
            {
                Age = -22,
                Name = "child name",
                Id = Guid.Parse("8bedccb3-ff93-47a2-9fc4-b558cae41a06"),
                Strings = new()
                {
                    "string1", "string2", "", "strangeString"
                }
            },
            Numbers = new() { 0, 1, -222, 1000, 22 }
        });
    }

    [Fact]
    public async Task BindingObjectFromQueryUse()
    {
        var (rsp, res) = await GuestClient
            .GETAsync<TestCases.QueryObjectBindingTest.Request, TestCases.QueryObjectBindingTest.Response>(
                "api/test-cases/query-object-binding-test" +
                "?BoOl=TRUE&String=everything&iNt=99&long=483752874564876&DOUBLE=2232.12&Enum=3" +
                "&age=45&name=john&id=10c225a6-9195-4596-92f5-c1234cee4de7" +
                "&numbers[0]=0&numbers[1]=1&numbers[2]=-222&numbers[3]=1000&numbers[4]=22" +
                "&favoriteDay=Friday&IsHidden=FALSE&ByteEnum=2" +
                "&child.id=8bedccb3-ff93-47a2-9fc4-b558cae41a06" +
                "&child.name=child name&child.age=-22" +
                "&CHILD.FavoriteDays[0]=1&ChiLD.FavoriteDays[1]=Saturday&CHILD.ISHiddeN=TruE" +
                "&child.strings[0]=string1&child.strings[1]=string2&child.strings[2]=&child.strings[3]=strangeString",
                new()
                {
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Should().BeEquivalentTo(
            new TestCases.QueryObjectBindingTest.Request
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
                    ByteEnum = TestCases.QueryObjectBindingTest.ByteEnum.AnotherCheck,
                    IsHidden = false,
                    Child = new()
                    {
                        Age = -22,
                        Name = "child name",
                        Id = Guid.Parse("8bedccb3-ff93-47a2-9fc4-b558cae41a06"),
                        Strings = new()
                        {
                            "string1", "string2", "", "strangeString"
                        },
                        FavoriteDays = new() { DayOfWeek.Monday, DayOfWeek.Saturday },
                        IsHidden = true
                    },
                    Numbers = new() { 0, 1, -222, 1000, 22 }
                }
            }
            );
    }

    [Fact]
    public async Task ByteArrayQueryParamBindingTestUse()
    {
        var (rsp, res) = await GuestClient
            .GETAsync<TestCases.ByteArrayQueryParamBindingTest.Request, TestCases.ByteArrayQueryParamBindingTest.Response>(
                "api/test-cases/byte-array-query-param-binding-test?timestamp=AAAAAAAAw1U%3D&timestamps=AAAAAAAAw1U%3D",

                new()
                {
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        System.Text.Json.JsonSerializer.Serialize(res!.Timestamp)
            .Should()
            .BeEquivalentTo("\"AAAAAAAAw1U=\"");

        System.Text.Json.JsonSerializer.Serialize(res!.ObjectWithByteArrays.Timestamp)
            .Should()
            .BeEquivalentTo("\"AAAAAAAAw1U=\"");

        System.Text.Json.JsonSerializer.Serialize(res!.ObjectWithByteArrays.Timestamps[0])
            .Should()
            .BeEquivalentTo("\"AAAAAAAAw1U=\"");
    }

    [Fact]
    public async Task BindingArraysOfObjectsFromQueryUse()
    {
        var (rsp, res) = await GuestClient
            .GETAsync<TestCases.QueryObjectWithObjectsArrayBindingTest.Request, TestCases.QueryObjectWithObjectsArrayBindingTest.Response>(
                "api/test-cases/query-arrays-of-objects-binding-test" +
                "?ArraysOfObjects[0][0].String=test&ArraysOfObjects[0][0].Bool=true&ArraysOfObjects[0][0].Double=22.22&ArraysOfObjects[0][0].Enum=4" +
                "&ArraysOfObjects[0][0].Int=31&ArraysOfObjects[0][0].Long=22" +
                "&ArraysOfObjects[0][1].String=test2&ArraysOfObjects[0][1].Enum=Wednesday" +
                "&ArraysOfObjects[1][0].String=test2&ArraysOfObjects[1][0].Enum=3" +
                "&Child.Objects[0].String=test&Child.Objects[0].Bool=true&Child.Objects[0].Double=22.22&Child.Objects[0].Enum=4" +
                "&Child.Objects[0].Int=31&Child.Objects[0].Long=22" +
                "&Child.Objects[1].String=test2&Child.Objects[1].Enum=Wednesday" +
                "&Objects[0].String=test&Objects[0].Bool=true&Objects[0].Double=22.22&Objects[0].Enum=4" +
                "&Objects[0].Int=31&Objects[0].Long=22" +
                "&Objects[1].String=test2&Objects[1].Enum=Wednesday",

                new()
                {
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Should().BeEquivalentTo(
            new TestCases.QueryObjectWithObjectsArrayBindingTest.Request
            {
                Person = new()
                {
                    ArraysOfObjects = new()
                    {
                        new TestCases.QueryObjectWithObjectsArrayBindingTest.ObjectInArray[]
                        {
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
                        },

                        new TestCases.QueryObjectWithObjectsArrayBindingTest.ObjectInArray[]
                        {
                            new()
                            {
                                String = "test2",
                                Enum = DayOfWeek.Wednesday
                            }
                        }
                    },
                    Child = new()
                    {
                        Objects = new()
                        {
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
                        }
                    },
                    Objects = new()
                    {
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
                    }
                }
            }
        );
    }

    [Fact]
    public async Task BindingFromAttributeUseSwaggerUiStyle()
    {
        var (rsp, res) = await GuestClient
            .POSTAsync<Request, Response>(
                "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45/" +
                "?Bool=false&String=everything&XBlank=256" +
                "&age=45&name=john&id=10c225a6-9195-4596-92f5-c1234cee4de7" +
                "&numbers=0&numbers=1&numbers=-222&numbers=1000&numbers=22" +
                "&child[id]=8bedccb3-ff93-47a2-9fc4-b558cae41a06" +
                "&child[name]=child name&child[age]=-22" +
                "&child[strings]=string1&child[strings]=string2&child[strings]=&child[strings]=strangeString",
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
                        Name = "wrong",
                    }
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.String.Should().Be("everything");
        res?.Bool.Should().BeFalse();
        res?.Int.Should().Be(99);
        res?.Long.Should().Be(483752874564876);
        res?.Double.Should().Be(2232.12);
        res?.FromBody.Should().Be("from body value");
        res?.Decimal.Should().Be(123.45m);
        res?.Blank.Should().Be(256);
        res?.Person.Should().NotBeNull();
        res?.Person.Should().BeEquivalentTo(new Person
        {
            Age = 45,
            Name = "john",
            Id = Guid.Parse("10c225a6-9195-4596-92f5-c1234cee4de7"),
            Child = new()
            {
                Age = -22,
                Name = "child name",
                Id = Guid.Parse("8bedccb3-ff93-47a2-9fc4-b558cae41a06"),
                Strings = new()
                {
                    "string1", "string2", "", "strangeString"
                }
            },
            Numbers = new() { 0, 1, -222, 1000, 22 }
        });
    }

    [Fact]

    public async Task BindingObjectFromQueryUseSwaggerUiStyle()
    {
        var (rsp, res) = await GuestClient
            .GETAsync<TestCases.QueryObjectBindingTest.Request, TestCases.QueryObjectBindingTest.Response>(
                "api/test-cases/query-object-binding-test" +
                "?BoOl=TRUE&String=everything&iNt=99&long=483752874564876&DOUBLE=2232.12&Enum=3" +
                "&age=45&name=john&id=10c225a6-9195-4596-92f5-c1234cee4de7" +
                "&numbers=0&numbers=1&numbers=-222&numbers=1000&numbers=22" +
                "&favoriteDay=Friday&IsHidden=FALSE&ByteEnum=2" +
                "&child[id]=8bedccb3-ff93-47a2-9fc4-b558cae41a06" +
                "&child[name]=child name&child[age]=-22" +
                "&CHILD[FavoriteDays]=1&ChiLD[FavoriteDays]=Saturday&CHILD[ISHiddeN]=TruE" +
                "&child[strings]=string1&child[strings]=string2&child[strings]=&child[strings]=strangeString",
                new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Should().BeEquivalentTo(
            new TestCases.QueryObjectBindingTest.Request
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
                    ByteEnum = TestCases.QueryObjectBindingTest.ByteEnum.AnotherCheck,
                    IsHidden = false,
                    Child = new()
                    {
                        Age = -22,
                        Name = "child name",
                        Id = Guid.Parse("8bedccb3-ff93-47a2-9fc4-b558cae41a06"),
                        Strings = new()
                        {
                            "string1", "string2", "", "strangeString"
                        },
                        FavoriteDays = new() { DayOfWeek.Monday, DayOfWeek.Saturday },
                        IsHidden = true
                    },
                    Numbers = new() { 0, 1, -222, 1000, 22 }
                }
            }
            );
    }

    [Fact]
    public async Task EventHandling()
    {
        var event1 = new NewItemAddedToStock { ID = 1, Name = "one", Quantity = 10 };
        var event2 = new NewItemAddedToStock { ID = 2, Name = "two", Quantity = 20 };
        var event3 = new NewItemAddedToStock { ID = 3, Name = "three", Quantity = 30 };

        await new Event<NewItemAddedToStock>().PublishAsync(event1, Mode.WaitForNone);
        await new Event<NewItemAddedToStock>().PublishAsync(event2, Mode.WaitForAny);
        await new Event<NewItemAddedToStock>().PublishAsync(event3, Mode.WaitForAll);

        event3.ID.Should().Be(0);
        event3.Name.Should().Be("pass");

        event2.ID.Should().Be(0);
        event2.Name.Should().Be("pass");

        event1.ID.Should().Be(0);
        event1.Name.Should().Be("pass");
    }

    [Fact]
    public async Task RangeHandling()
    {
        var res = await RangeClient.GetStringAsync("api/test-cases/range");
        res.Should().Be("fghij");
    }

    [Fact]
    public async Task FileHandling()
    {
        using var imageContent = new ByteArrayContent(
            await new StreamContent(
                    File.OpenRead("test.png"))
                .ReadAsByteArrayAsync());
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        using var form = new MultipartFormDataContent
        {
            {imageContent, "File", "test.png"},
            {new StringContent("500"), "Width"},
            {new StringContent("500"), "Height"}
        };

        var res = await AdminClient.PostAsync("api/uploads/image/save", form);

        using var md5Instance = MD5.Create();
        using var stream = await res.Content.ReadAsStreamAsync();
        var resMD5 = BitConverter.ToString(md5Instance.ComputeHash(stream)).Replace("-", "");

        resMD5.Should().Be("8A1F6A8E27D2E440280050DA549CBE3E");
    }

    [Fact]
    public async Task FileHandlingFileBinding()
    {
        using var stream1 = File.OpenRead("test.png");
        using var stream2 = File.OpenRead("test.png");

        var req = new Uploads.Image.SaveTyped.Request
        {
            File1 = new FormFile(stream1, 0, stream1.Length, "File1", "test.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            },
            File2 = new FormFile(stream2, 0, stream2.Length, "File2", "test.png"),
            Width = 500,
            Height = 500
        };

        var res = await AdminClient.POSTAsync<
            Uploads.Image.SaveTyped.Endpoint,
            Uploads.Image.SaveTyped.Request>(req, sendAsFormData: true);

        using var md5Instance = MD5.Create();
        using var stream = await res.Content.ReadAsStreamAsync();
        var resMD5 = BitConverter.ToString(md5Instance.ComputeHash(stream)).Replace("-", "");

        resMD5.Should().Be("8A1F6A8E27D2E440280050DA549CBE3E");
    }

    [Fact]
    public async Task PreProcessorShortCircuitingWhileValidatorFails()
    {
        var x = await GuestClient.GETAsync<
            TestCases.PrecessorShortWhileValidatorFails.Endpoint,
            TestCases.PrecessorShortWhileValidatorFails.Request,
            object>(new()
            {
                Id = 0
            });

        x.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        x.Result?.ToString().Should().Be("hello from pre-processor!");
    }

    [Fact]
    public async Task PreProcessorsAreRunIfValidationFailuresOccur()
    {
        var (rsp, res) = await AdminClient.POSTAsync<
            TestCases.PreProcessorIsRunOnValidationFailure.Endpoint,
            TestCases.PreProcessorIsRunOnValidationFailure.Request,
            ErrorResponse>
        (new()
        {
            FailureCount = 0,
            FirstName = ""
        });

        rsp?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res?.Errors.Should().NotBeNull();
        res?.Errors.Count.Should().Be(2);
        res?.Errors["x"].First().Should().Be("blah");
    }

    [Fact]
    public async Task OnBeforeOnAfterValidation()
    {
        var (rsp, res) = await AdminClient.POSTAsync<
            TestCases.OnBeforeAfterValidationTest.Endpoint,
            TestCases.OnBeforeAfterValidationTest.Request,
            TestCases.OnBeforeAfterValidationTest.Response>(new()
            {
                Host = "blah",
                Verb = Http.DELETE
            });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Host.Should().Be("localhost");
    }

    [Fact]
    public async Task PreProcessorShortCircuitMissingHeader()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            Sales.Orders.Retrieve.Endpoint,
            Sales.Orders.Retrieve.Request,
            ErrorResponse>(new() { OrderID = "order1" });

        rsp?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res?.Errors.Should().NotBeNull();
        res?.Errors.Count.Should().Be(1);
        res?.Errors.Should().ContainKey("MissingHeaders");
    }

    [Fact]
    public async Task PreProcessorShortCircuitWrongHeaderValue()
    {
        var (rsp, res) = await AdminClient.POSTAsync<
            Sales.Orders.Retrieve.Endpoint,
            Sales.Orders.Retrieve.Request,
            object>(new()
            {
                OrderID = "order1"
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task PreProcessorShortCircuitHandlerExecuted()
    {
        var (rsp, res) = await CustomerClient.GETAsync<
            Sales.Orders.Retrieve.Endpoint,
            Sales.Orders.Retrieve.Request,
            ErrorResponse>(new() { OrderID = "order1" });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Message.Should().Be("ok!");
    }

    [Fact]
    public async Task ProcessorStateWorks()
    {
        var x = await GuestClient.GETAsync<
            TestCases.ProcessorStateTest.Endpoint,
            TestCases.ProcessorStateTest.Request,
            string>(new() { Id = 10101 });

        x.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        x.Result.Should().Be("10101 jane doe");
    }

    [Fact]
    public async Task PlainTextBodyModelBinding()
    {
        using var stringContent = new StringContent("this is the body content");
        stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var rsp = await AdminClient.PostAsync("test-cases/plaintext/12345", stringContent);

        var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>();

        res?.BodyContent.Should().Be("this is the body content");
        res?.Id.Should().Be(12345);
    }

    [Fact]
    public async Task GlobalRoutePrefixOverride()
    {
        using var stringContent = new StringContent("this is the body content");
        stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var rsp = await AdminClient.PostAsync("/mobile/api/test-cases/global-prefix-override/12345", stringContent);

        var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>();

        res?.BodyContent.Should().Be("this is the body content");
        res?.Id.Should().Be(12345);
    }

    [Fact]
    public async Task GETRequestWithRouteParameterAndReqDto()
    {
        var (rsp, res) = await CustomerClient.GETAsync<EmptyRequest, ErrorResponse>(
            "/api/sales/orders/retrieve/54321",
            new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Message.Should().Be("ok!");
    }

    [Fact]
    public async Task QueryParamReadingInEndpointWithoutRequest()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            EmptyRequest,
            TestCases.QueryParamBindingInEpWithoutReq.Response>(
            "/api/test-cases/ep-witout-req-query-param-binding-test" +
            "?customerId=09809" +
            "&otherId=12" +
            "&doubles=[123.45,543.21]" +
            "&guids=[\"b01ec302-0adc-4a2b-973d-bbfe639ed9a5\",\"e08664a4-efd8-4062-a1e1-6169c6eac2ab\"]" +
            "&ints=[1,2,3]" +
            "&floaty=3.2", new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res!.CustomerID.Should().Be(09809);
        res!.OtherID.Should().Be(12);
        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Doubles.Length.Should().Be(2);
        res?.Doubles[0].Should().Be(123.45);
        res?.Guids.Count.Should().Be(2);
        res?.Guids[0].Should().Be(Guid.Parse("b01ec302-0adc-4a2b-973d-bbfe639ed9a5"));
        res?.Ints.Count().Should().Be(3);
        res?.Ints.First().Should().Be(1);
    }

    [Fact]
    public async Task QueryParamReadingIsRequired()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            EmptyRequest,
            ErrorResponse>(
            "/api/test-cases/ep-witout-req-query-param-binding-test?customerId=09809&otherId=lkjhlkjh", new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res?.Errors.Should().ContainKey("OtherID");
    }

    [Fact]
    public async Task ThrottledGlobalResponse()
    {
        HttpResponseMessage? response = null;

        for (var i = 0; i < 5; i++)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get
            };
            request.Headers.Add("X-Custom-Throttle-Header", "test");
            request.RequestUri =
                new Uri("api/test-cases/global-throttle-error-response?customerId=09809&otherId=12",
                    UriKind.Relative);
            response = await GuestClient.SendAsync(request);
        }

        var responseContent = await response!.Content.ReadAsStringAsync();
        responseContent.Should().Be("Custom Error Response");
        response!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task NotThrottledGlobalResponse()
    {
        HttpResponseMessage? response = null;

        for (var i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get
            };
            request.Headers.Add("X-Custom-Throttle-Header", "test-2");
            request.RequestUri =
                new Uri("api/test-cases/global-throttle-error-response?customerId=09809&otherId=12",
                    UriKind.Relative);
            response = await GuestClient.SendAsync(request);
        }

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FromBodyJsonBinding()
    {
        var (rsp, res) = await CustomerClient.POSTAsync<
            TestCases.FromBodyJsonBinding.Endpoint,
            TestCases.FromBodyJsonBinding.Product,
            TestCases.FromBodyJsonBinding.Response>(new()
            {
                Id = 202,
                Name = "test product",
                Price = 10.10m
            });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Product.Name.Should().Be("test product");
        res?.Product.Price.Should().Be(10.10m);
        res?.Product.Id.Should().Be(202);
        res?.CustomerID.Should().Be(123);
        res?.Id.Should().Be(0);
    }

    [Fact]
    public async Task CustomRequestBinder()
    {
        var (rsp, res) = await CustomerClient.POSTAsync<
            TestCases.CustomRequestBinder.Endpoint,
            TestCases.CustomRequestBinder.Product,
            TestCases.CustomRequestBinder.Response>(new()
            {
                Id = 202,
                Name = "test product",
                Price = 10.10m
            });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Product?.Name.Should().Be("test product");
        res?.Product?.Price.Should().Be(10.10m);
        res?.Product?.Id.Should().Be(202);
        res?.CustomerID.Should().Be("123");
        res?.Id.Should().Be(null);
    }

    [Fact]
    public async Task DontCatchExceptions()
    {
        try
        {
            await GuestClient.GetStringAsync("/api/test-cases/one");
        }
        catch { }

        var res = await GuestClient.GetStringAsync("/api/test-cases/1");

        res.Should().Be("1");
    }
}