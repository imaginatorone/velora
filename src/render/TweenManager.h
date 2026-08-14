
#pragma once

#include <string>
#include <vector>
#include <functional>
#include <cmath>
#include <cstdint>
#include <numbers>

namespace velora::render {

namespace easing {
inline float linear(float t) {
    return t;
}
inline float easeInQuad(float t) {
    return t * t;
}
inline float easeOutQuad(float t) {
    return t * (2.0f - t);
}
inline float easeInOutQuad(float t) {
    return t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
}
inline float easeInCubic(float t) {
    return t * t * t;
}
inline float easeOutCubic(float t) {
    float f = t - 1;
    return f * f * f + 1;
}
inline float easeInOutCubic(float t) {
    return t < 0.5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
}
inline float easeOutElastic(float t) {
    if (t == 0 || t == 1)
        return t;
    return std::pow(2.0f, -10.0f * t) *
               std::sin((t - 0.075f) * (2.0f * std::numbers::pi_v<float>) / 0.3f) +
           1.0f;
}
inline float easeOutBounce(float t) {
    if (t < 1 / 2.75f)
        return 7.5625f * t * t;
    if (t < 2 / 2.75f) {
        t -= 1.5f / 2.75f;
        return 7.5625f * t * t + 0.75f;
    }
    if (t < 2.5f / 2.75f) {
        t -= 2.25f / 2.75f;
        return 7.5625f * t * t + 0.9375f;
    }
    t -= 2.625f / 2.75f;
    return 7.5625f * t * t + 0.984375f;
}
inline float easeOutBack(float t) {
    const float s = 1.70158f;
    float f = t - 1.0f;
    return f * f * ((s + 1) * f + s) + 1;
}

using EasingFn = float (*)(float);
inline EasingFn fromName(const std::string& name) {
    if (name == "linear")
        return linear;
    if (name == "ease_in")
        return easeInCubic;
    if (name == "ease_out")
        return easeOutCubic;
    if (name == "ease_in_out")
        return easeInOutCubic;
    if (name == "bounce")
        return easeOutBounce;
    if (name == "elastic")
        return easeOutElastic;
    if (name == "back")
        return easeOutBack;
    return easeOutCubic;
}
} // namespace easing

struct Tween {
    std::string id;
    std::string target;
    std::string property;
    float fromValue;
    float toValue;
    float duration;
    float elapsed = 0.0f;
    easing::EasingFn easingFn = easing::easeOutCubic;
    bool finished = false;
    bool autoRemove = true;
    std::function<void(float)> onUpdate;
    std::function<void()> onComplete;
};

class TweenManager {
  public:
    TweenManager();
    ~TweenManager();

    void update(double dt);

    std::string createTween(const std::string& target, const std::string& property, float from,
                            float to, float duration, const std::string& easingName = "ease_out");

    void setOnUpdate(const std::string& tweenId, std::function<void(float)> fn);
    void setOnComplete(const std::string& tweenId, std::function<void()> fn);

    void cancelTween(const std::string& tweenId);

    void cancelAllFor(const std::string& target);

    void cancelAll();

    void setEnabled(bool enabled) { m_enabled = enabled; }
    [[nodiscard]] bool isEnabled() const { return m_enabled; }

  private:
    std::vector<Tween> m_tweens;
    uint64_t m_nextId = 0;
    bool m_enabled = true;
};

} // namespace velora::render
