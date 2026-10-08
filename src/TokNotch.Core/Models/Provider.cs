namespace TokNotch.Core.Models;

public enum Provider { Claude, OpenAI, Gemini, Unknown, Dsh, Kimi, Mimo, DeepSeek, Qwen }
public enum DetectionState { NotDetected, DetectedNoUsage, Ready, Unavailable, Disabled }
public enum CostStatus { Complete, Partial, Unavailable, Stale }
public enum RefreshState { Ready, Refreshing, Failed }
