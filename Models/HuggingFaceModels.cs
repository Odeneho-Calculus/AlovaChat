using System.Text.Json.Serialization;

namespace AlovaChat.Models;

public class HuggingFaceRequest
{
    [JsonPropertyName("inputs")]
    public string Inputs { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public HuggingFaceParameters? Parameters { get; set; }

    [JsonPropertyName("options")]
    public HuggingFaceOptions? Options { get; set; }
}

public class HuggingFaceParameters
{
    [JsonPropertyName("max_new_tokens")]
    public int? MaxNewTokens { get; set; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("repetition_penalty")]
    public float? RepetitionPenalty { get; set; }

    [JsonPropertyName("do_sample")]
    public bool? DoSample { get; set; }

    [JsonPropertyName("return_full_text")]
    public bool? ReturnFullText { get; set; }
}

public class HuggingFaceOptions
{
    [JsonPropertyName("wait_for_model")]
    public bool WaitForModel { get; set; } = true;

    [JsonPropertyName("use_cache")]
    public bool UseCache { get; set; } = true;
}

public class HuggingFaceResponse
{
    [JsonPropertyName("generated_text")]
    public string? GeneratedText { get; set; }

    [JsonPropertyName("conversation")]
    public HuggingFaceConversation? Conversation { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("estimated_time")]
    public float? EstimatedTime { get; set; }
}

public class HuggingFaceConversation
{
    [JsonPropertyName("generated_responses")]
    public string[]? GeneratedResponses { get; set; }

    [JsonPropertyName("past_user_inputs")]
    public string[]? PastUserInputs { get; set; }
}

public class HuggingFaceErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("estimated_time")]
    public float? EstimatedTime { get; set; }

    [JsonPropertyName("warnings")]
    public string[]? Warnings { get; set; }
}

public class HuggingFaceModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("pipeline_tag")]
    public string PipelineTag { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }

    [JsonPropertyName("likes")]
    public int Likes { get; set; }

    [JsonPropertyName("library_name")]
    public string? LibraryName { get; set; }
}