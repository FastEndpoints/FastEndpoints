using System.Text.Json.Nodes;
using FastEndpoints.OpenApi;
using Microsoft.OpenApi;

namespace OpenApi;

public class HttpFileExporterTests
{
    [Fact]
    public void top_level_ref_body_expands_property_keys()
    {
        var document = CreateDocument(
            operationId: "Login",
            path: "/login",
            bodySchemaRef: "LoginRequest",
            schemas: new()
            {
                ["LoginRequest"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["userName"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        ["password"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            });

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "Login");

        body["userName"]!.GetValue<string>().ShouldBe("");
        body["password"]!.GetValue<string>().ShouldBe("");
    }

    [Fact]
    public void dual_sibling_refs_to_same_component_both_expand()
    {
        var document = CreateDocument(
            operationId: "DualChild",
            path: "/dual-child",
            bodySchemaRef: "DualChildRequest",
            schemas: new()
            {
                ["DualChildAddress"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["zip"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                },
                ["DualChildRequest"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["billingAddress"] = new OpenApiSchemaReference("DualChildAddress"),
                        ["shippingAddress"] = new OpenApiSchemaReference("DualChildAddress")
                    }
                }
            });

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "DualChild");

        body["billingAddress"]!["zip"]!.GetValue<string>().ShouldBe("");
        body["shippingAddress"]!["zip"]!.GetValue<string>().ShouldBe("");
    }

    [Fact]
    public void nullable_ref_oneOf_unwraps_and_expands()
    {
        var document = CreateDocument(
            operationId: "NullableRef",
            path: "/nullable-ref",
            bodySchemaRef: "Wrapper",
            schemas: new()
            {
                ["Address"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["street"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                },
                ["Wrapper"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["address"] = new OpenApiSchema
                        {
                            OneOf =
                            [
                                new OpenApiSchema { Type = JsonSchemaType.Null },
                                new OpenApiSchemaReference("Address")
                            ]
                        }
                    }
                }
            });

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "NullableRef");

        body["address"]!["street"]!.GetValue<string>().ShouldBe("");
    }

    [Fact]
    public void cyclic_schema_terminates_without_throw()
    {
        var nodeSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>()
        };
        nodeSchema.Properties!["child"] = new OpenApiSchemaReference("Node");

        var document = CreateDocument(
            operationId: "Cycle",
            path: "/cycle",
            bodySchemaRef: "Node",
            schemas: new()
            {
                ["Node"] = nodeSchema
            });

        var http = HttpFileExporter.ToHttpFileContent(document);
        var body = ExtractJsonBody(http, "Cycle");

        // cycle short-circuits the recursive property to null; root remains an object (never literal null body)
        body.ShouldNotBeNull();
        body.AsObject().ContainsKey("child").ShouldBeTrue();
        body["child"].ShouldBeNull();
    }

    [Fact]
    public void empty_object_schema_emits_empty_object_not_null_root()
    {
        var document = CreateDocument(
            operationId: "Empty",
            path: "/empty",
            bodySchemaRef: null,
            inlineBodySchema: new OpenApiSchema { Type = JsonSchemaType.Object },
            schemas: new());

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "Empty");

        body.ShouldNotBeNull();
        body.AsObject().Count.ShouldBe(0);
    }

    [Fact]
    public void multi_branch_oneOf_polymorphism_emits_empty_object()
    {
        var document = CreateDocument(
            operationId: "Poly",
            path: "/poly",
            bodySchemaRef: null,
            inlineBodySchema: new OpenApiSchema
            {
                OneOf =
                [
                    new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["a"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    },
                    new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["b"] = new OpenApiSchema { Type = JsonSchemaType.Integer }
                        }
                    }
                ]
            },
            schemas: new());

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "Poly");

        body.ShouldNotBeNull();
        body.AsObject().Count.ShouldBe(0);
    }

    [Fact]
    public void base_url_comes_from_document_servers()
    {
        var document = new OpenApiDocument
        {
            Servers = [new() { Url = "http://localhost/" }],
            Paths = new()
            {
                ["/ping"] = new OpenApiPathItem
                {
                    Operations = new()
                    {
                        [HttpMethod.Get] = new() { OperationId = "Ping" }
                    }
                }
            }
        };

        var http = HttpFileExporter.ToHttpFileContent(document);

        http.ShouldStartWith("@baseUrl = http://localhost\n");
    }

    [Fact]
    public void multipart_form_data_omits_json_body()
    {
        var document = new OpenApiDocument
        {
            Paths = new()
            {
                ["/upload"] = new OpenApiPathItem
                {
                    Operations = new()
                    {
                        [HttpMethod.Post] = new()
                        {
                            OperationId = "Upload",
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["multipart/form-data"] = new()
                                    {
                                        Schema = new OpenApiSchema
                                        {
                                            Type = JsonSchemaType.Object,
                                            Properties = new Dictionary<string, IOpenApiSchema>
                                            {
                                                ["file"] = new OpenApiSchema { Type = JsonSchemaType.String }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var http = HttpFileExporter.ToHttpFileContent(document);

        http.ShouldContain("Content-Type: multipart/form-data");
        http.ShouldContain("# body omitted (multipart/form-data); provide form fields in the client");
        http.ShouldNotContain("\"file\"");
    }

    [Fact]
    public void text_plain_body_uses_body_variable()
    {
        var document = new OpenApiDocument
        {
            Paths = new()
            {
                ["/text"] = new OpenApiPathItem
                {
                    Operations = new()
                    {
                        [HttpMethod.Post] = new()
                        {
                            OperationId = "TextBody",
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["text/plain"] = new()
                                    {
                                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var http = HttpFileExporter.ToHttpFileContent(document);

        http.ShouldContain("Content-Type: text/plain");
        http.ShouldContain("{{body}}");
    }

    [Fact]
    public void plus_json_content_type_still_builds_json_skeleton()
    {
        var document = new OpenApiDocument
        {
            Paths = new()
            {
                ["/patch"] = new OpenApiPathItem
                {
                    Operations = new()
                    {
                        [HttpMethod.Patch] = new()
                        {
                            OperationId = "JsonPatch",
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json-patch+json"] = new()
                                    {
                                        Schema = new OpenApiSchema
                                        {
                                            Type = JsonSchemaType.Array,
                                            Items = new OpenApiSchema { Type = JsonSchemaType.Object }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var http = HttpFileExporter.ToHttpFileContent(document);

        http.ShouldContain("Content-Type: application/json-patch+json");
        http.ShouldContain("[\n  {}\n]");
    }

    [Fact]
    public void cookie_parameters_emit_cookie_header()
    {
        var document = new OpenApiDocument
        {
            Paths = new()
            {
                ["/cookie-get"] = new OpenApiPathItem
                {
                    Operations = new()
                    {
                        [HttpMethod.Get] = new()
                        {
                            OperationId = "CookieGet",
                            Parameters =
                            [
                                new OpenApiParameter
                                {
                                    Name = "session_id",
                                    In = ParameterLocation.Cookie
                                },
                                new OpenApiParameter
                                {
                                    Name = "theme",
                                    In = ParameterLocation.Cookie
                                }
                            ]
                        }
                    }
                }
            }
        };

        var http = HttpFileExporter.ToHttpFileContent(document);

        http.ShouldContain("Cookie: session_id={{session_id}}; theme={{theme}}");
    }

    [Fact]
    public void bearer_security_emits_authorization_placeholder()
    {
        var document = new OpenApiDocument
        {
            Paths = new()
            {
                ["/secure"] = new OpenApiPathItem
                {
                    Operations = new()
                    {
                        [HttpMethod.Post] = new()
                        {
                            OperationId = "SecureOp",
                            Security =
                            [
                                new()
                                {
                                    [new("JWTBearerAuth")] = []
                                }
                            ]
                        }
                    }
                }
            },
            Components = new()
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["JWTBearerAuth"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "Bearer",
                        BearerFormat = "JWT"
                    }
                }
            }
        };

        var http = HttpFileExporter.ToHttpFileContent(document);

        http.ShouldContain("Authorization: Bearer {{bearerToken}}");
    }

    [Fact]
    public void existing_authorization_header_parameter_skips_bearer_placeholder()
    {
        var document = new OpenApiDocument
        {
            Paths = new()
            {
                ["/secure"] = new OpenApiPathItem
                {
                    Operations = new()
                    {
                        [HttpMethod.Get] = new()
                        {
                            OperationId = "SecureWithHeader",
                            Parameters =
                            [
                                new OpenApiParameter
                                {
                                    Name = "Authorization",
                                    In = ParameterLocation.Header
                                }
                            ],
                            Security =
                            [
                                new()
                                {
                                    [new("JWTBearerAuth")] = []
                                }
                            ]
                        }
                    }
                }
            },
            Components = new()
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["JWTBearerAuth"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "Bearer"
                    }
                }
            }
        };

        var http = HttpFileExporter.ToHttpFileContent(document);

        http.ShouldContain("Authorization: {{Authorization}}");
        http.ShouldNotContain("Bearer {{bearerToken}}");
    }

    [Fact]
    public void components_fallback_expands_ref_when_host_document_unset()
    {
        // OpenApiSchemaReference without HostDocument — ResolveSchema alone fails; exporter components fallback must still expand.
        var document = new OpenApiDocument
        {
            Paths = new()
            {
                ["/login"] = new OpenApiPathItem
                {
                    Operations = new()
                    {
                        [HttpMethod.Post] = new()
                        {
                            OperationId = "LoginNoHost",
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json"] = new()
                                    {
                                        Schema = new OpenApiSchemaReference("LoginRequest") // no HostDocument
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Components = new()
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["LoginRequest"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["userName"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["password"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    }
                }
            }
        };

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "LoginNoHost");

        body["userName"]!.GetValue<string>().ShouldBe("");
        body["password"]!.GetValue<string>().ShouldBe("");
    }

    [Fact]
    public void media_type_example_used_as_full_body()
    {
        var document = CreateDocument(
            operationId: "MediaExample",
            path: "/media-example",
            bodySchemaRef: "LoginRequest",
            schemas: LoginRequestSchemas(),
            mediaExample: JsonNode.Parse("""{"userName":"a","password":"b"}"""));

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "MediaExample");

        body["userName"]!.GetValue<string>().ShouldBe("a");
        body["password"]!.GetValue<string>().ShouldBe("b");
    }

    [Fact]
    public void media_type_named_examples_first_value_used_when_example_null()
    {
        var document = CreateDocument(
            operationId: "NamedExamples",
            path: "/named-examples",
            bodySchemaRef: "LoginRequest",
            schemas: LoginRequestSchemas(),
            mediaExamples: new Dictionary<string, IOpenApiExample>
            {
                ["good"] = new OpenApiExample { Value = JsonNode.Parse("""{"userName":"good","password":"g"}""") },
                ["bad"] = new OpenApiExample { Value = JsonNode.Parse("""{"userName":"bad","password":"b"}""") }
            });

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "NamedExamples");

        body["userName"]!.GetValue<string>().ShouldBe("good");
        body["password"]!.GetValue<string>().ShouldBe("g");
    }

    [Fact]
    public void schema_example_used_when_media_has_no_example()
    {
        var schemas = LoginRequestSchemas();
        ((OpenApiSchema)schemas["LoginRequest"]).Example = JsonNode.Parse("""{"userName":"schema","password":"ex"}""");

        var document = CreateDocument(
            operationId: "SchemaExample",
            path: "/schema-example",
            bodySchemaRef: "LoginRequest",
            schemas: schemas);

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "SchemaExample");

        body["userName"]!.GetValue<string>().ShouldBe("schema");
        body["password"]!.GetValue<string>().ShouldBe("ex");
    }

    [Fact]
    public void property_default_fills_field_when_no_examples()
    {
        var document = CreateDocument(
            operationId: "PropDefault",
            path: "/prop-default",
            bodySchemaRef: "LoginRequest",
            schemas: new()
            {
                ["LoginRequest"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["userName"] = new OpenApiSchema { Type = JsonSchemaType.String, Default = JsonValue.Create("x") },
                        ["password"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            });

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "PropDefault");

        body["userName"]!.GetValue<string>().ShouldBe("x");
        body["password"]!.GetValue<string>().ShouldBe("");
    }

    [Fact]
    public void property_example_wins_over_property_default()
    {
        var document = CreateDocument(
            operationId: "PropExampleWins",
            path: "/prop-example-wins",
            bodySchemaRef: "LoginRequest",
            schemas: new()
            {
                ["LoginRequest"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["userName"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Example = JsonValue.Create("from-example"),
                            Default = JsonValue.Create("from-default")
                        },
                        ["password"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            });

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "PropExampleWins");

        body["userName"]!.GetValue<string>().ShouldBe("from-example");
    }

    [Fact]
    public void media_example_wins_over_property_defaults()
    {
        var document = CreateDocument(
            operationId: "MediaBeatsProps",
            path: "/media-beats-props",
            bodySchemaRef: "LoginRequest",
            schemas: new()
            {
                ["LoginRequest"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["userName"] = new OpenApiSchema { Type = JsonSchemaType.String, Default = JsonValue.Create("default-user") },
                        ["password"] = new OpenApiSchema { Type = JsonSchemaType.String, Default = JsonValue.Create("default-pass") }
                    }
                }
            },
            mediaExample: JsonNode.Parse("""{"userName":"media-user","password":"media-pass"}"""));

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "MediaBeatsProps");

        body["userName"]!.GetValue<string>().ShouldBe("media-user");
        body["password"]!.GetValue<string>().ShouldBe("media-pass");
    }

    [Fact]
    public void dual_sibling_refs_both_get_target_property_example()
    {
        var document = CreateDocument(
            operationId: "DualChildExample",
            path: "/dual-child-example",
            bodySchemaRef: "DualChildRequest",
            schemas: new()
            {
                ["DualChildAddress"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["zip"] = new OpenApiSchema { Type = JsonSchemaType.String, Example = JsonValue.Create("90210") }
                    }
                },
                ["DualChildRequest"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["billingAddress"] = new OpenApiSchemaReference("DualChildAddress"),
                        ["shippingAddress"] = new OpenApiSchemaReference("DualChildAddress")
                    }
                }
            });

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document), "DualChildExample");

        body["billingAddress"]!["zip"]!.GetValue<string>().ShouldBe("90210");
        body["shippingAddress"]!["zip"]!.GetValue<string>().ShouldBe("90210");
    }

    static Dictionary<string, IOpenApiSchema> LoginRequestSchemas()
        => new()
        {
            ["LoginRequest"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["userName"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["password"] = new OpenApiSchema { Type = JsonSchemaType.String }
                }
            }
        };

    static OpenApiDocument CreateDocument(string operationId,
                                          string path,
                                          string? bodySchemaRef,
                                          Dictionary<string, IOpenApiSchema> schemas,
                                          IOpenApiSchema? inlineBodySchema = null,
                                          JsonNode? mediaExample = null,
                                          IDictionary<string, IOpenApiExample>? mediaExamples = null)
    {
        var document = new OpenApiDocument
        {
            Paths = new(),
            Components = new() { Schemas = schemas }
        };

        var bodySchema = inlineBodySchema ?? new OpenApiSchemaReference(bodySchemaRef!, document);

        document.Paths[path] = new OpenApiPathItem
        {
            Operations = new()
            {
                [HttpMethod.Post] = new()
                {
                    OperationId = operationId,
                    RequestBody = new OpenApiRequestBody
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new()
                            {
                                Schema = bodySchema,
                                Example = mediaExample,
                                Examples = mediaExamples
                            }
                        }
                    }
                }
            }
        };

        return document;
    }

    internal static JsonObject ExtractJsonBody(string http, string operationId)
    {
        var section = ExtractOperationSection(http, operationId);
        var blankLine = section.IndexOf("\n\n", StringComparison.Ordinal);
        blankLine.ShouldBeGreaterThanOrEqualTo(0, $"headers/body separator missing for {operationId}\n{section}");

        var bodyText = section[(blankLine + 2)..].Trim();
        bodyText.ShouldNotBeNullOrWhiteSpace($"empty body for {operationId}\n{section}");

        return JsonNode.Parse(bodyText)!.AsObject();
    }

    internal static string ExtractOperationSection(string http, string operationId)
    {
        var marker = $"### {operationId}\n";
        var start = http.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"operation {operationId} not found in:\n{http}");

        var contentStart = start + marker.Length;
        var next = http.IndexOf("\n### ", contentStart, StringComparison.Ordinal);

        return next < 0 ? http[contentStart..] : http[contentStart..next];
    }
}

public class HttpExportRegressionTests(Fixture App) : TestBase<Fixture>
{
    [Fact]
    public async Task admin_login_body_has_username_and_password()
    {
        var http = await App.GetHttpFileContentAsync("Initial Release", Cancellation);
        var body = HttpFileExporterTests.ExtractJsonBody(http, "PostAdminLoginEndpoint");

        body["userName"]!.GetValue<string>().ShouldBe("custom example user name from summary");
        body["password"]!.GetValue<string>().ShouldBe("custom example password from summary");
    }

    [Fact]
    public async Task create_inventory_item_expands_request_props()
    {
        var http = await App.GetHttpFileContentAsync("Initial Release", Cancellation);
        var body = HttpFileExporterTests.ExtractJsonBody(http, "CreateInventoryItem");

        body.ContainsKey("id").ShouldBeTrue();
        body.ContainsKey("name").ShouldBeTrue();
        body.ContainsKey("description").ShouldBeTrue();
        body.ContainsKey("price").ShouldBeTrue();
        body.ContainsKey("qtyOnHand").ShouldBeTrue();
        body.ContainsKey("modifiedBy").ShouldBeTrue();
        body.ContainsKey("generateFullUrl").ShouldBeTrue();
        body["id"]!.GetValue<int>().ShouldBe(1);
        body["name"]!.GetValue<string>().ShouldBe("first name");
        body["description"]!.GetValue<string>().ShouldBe("first description");
        body["price"]!.GetValue<int>().ShouldBe(10);
        body["qtyOnHand"]!.GetValue<int>().ShouldBe(10);
        body["modifiedBy"]!.GetValue<string>().ShouldBe("modifiedBy");
        body["generateFullUrl"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task dual_child_address_expands_both_addresses()
    {
        var http = await App.GetHttpFileContentAsync("Initial Release", Cancellation);
        var body = HttpFileExporterTests.ExtractJsonBody(http, "swagger_review_TestCasesSwaggerReviewDualChildAddressEndpoint");

        body["billingAddress"]!["zip"]!.GetValue<string>().ShouldBe("");
        body["shippingAddress"]!["zip"]!.GetValue<string>().ShouldBe("");
    }

    [Fact]
    public async Task cookie_get_emits_cookie_header()
    {
        var http = await App.GetHttpFileContentAsync("Initial Release", Cancellation);
        var section = HttpFileExporterTests.ExtractOperationSection(http, "swagger_review_TestCasesSwaggerReviewCookieGetReviewEndpoint");

        section.ShouldContain("Cookie: session_id={{session_id}}");
    }

    [Fact]
    public async Task create_inventory_item_emits_bearer_authorization()
    {
        var http = await App.GetHttpFileContentAsync("Initial Release", Cancellation);
        var section = HttpFileExporterTests.ExtractOperationSection(http, "CreateInventoryItem");

        section.ShouldContain("Authorization: Bearer {{bearerToken}}");
    }
}