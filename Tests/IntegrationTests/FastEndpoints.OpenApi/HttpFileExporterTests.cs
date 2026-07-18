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
            schemas: new Dictionary<string, IOpenApiSchema>
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

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document, "test"), "Login");

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
            schemas: new Dictionary<string, IOpenApiSchema>
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

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document, "test"), "DualChild");

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
            schemas: new Dictionary<string, IOpenApiSchema>
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

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document, "test"), "NullableRef");

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
            schemas: new Dictionary<string, IOpenApiSchema>
            {
                ["Node"] = nodeSchema
            });

        var http = HttpFileExporter.ToHttpFileContent(document, "test");
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
            schemas: new Dictionary<string, IOpenApiSchema>());

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document, "test"), "Empty");

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
            schemas: new Dictionary<string, IOpenApiSchema>());

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document, "test"), "Poly");

        body.ShouldNotBeNull();
        body.AsObject().Count.ShouldBe(0);
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
                        [HttpMethod.Get] = new OpenApiOperation
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

        var http = HttpFileExporter.ToHttpFileContent(document, "test");

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
                        [HttpMethod.Post] = new OpenApiOperation
                        {
                            OperationId = "SecureOp",
                            Security =
                            [
                                new OpenApiSecurityRequirement
                                {
                                    [new OpenApiSecuritySchemeReference("JWTBearerAuth")] = []
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

        var http = HttpFileExporter.ToHttpFileContent(document, "test");

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
                        [HttpMethod.Get] = new OpenApiOperation
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
                                new OpenApiSecurityRequirement
                                {
                                    [new OpenApiSecuritySchemeReference("JWTBearerAuth")] = []
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

        var http = HttpFileExporter.ToHttpFileContent(document, "test");

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
                        [HttpMethod.Post] = new OpenApiOperation
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

        var body = ExtractJsonBody(HttpFileExporter.ToHttpFileContent(document, "test"), "LoginNoHost");

        body["userName"]!.GetValue<string>().ShouldBe("");
        body["password"]!.GetValue<string>().ShouldBe("");
    }

    static OpenApiDocument CreateDocument(string operationId,
                                          string path,
                                          string? bodySchemaRef,
                                          Dictionary<string, IOpenApiSchema> schemas,
                                          IOpenApiSchema? inlineBodySchema = null)
    {
        var document = new OpenApiDocument
        {
            Paths = new(),
            Components = new() { Schemas = schemas }
        };

        IOpenApiSchema bodySchema = inlineBodySchema ?? new OpenApiSchemaReference(bodySchemaRef!, document);

        document.Paths[path] = new OpenApiPathItem
        {
            Operations = new()
            {
                [HttpMethod.Post] = new OpenApiOperation
                {
                    OperationId = operationId,
                    RequestBody = new OpenApiRequestBody
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new() { Schema = bodySchema }
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

        body["userName"]!.GetValue<string>().ShouldBe("");
        body["password"]!.GetValue<string>().ShouldBe("");
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
        body["id"]!.GetValue<int>().ShouldBe(0);
        body["name"]!.GetValue<string>().ShouldBe("");
        body["price"]!.GetValue<int>().ShouldBe(0);
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
