
#include "TweenManager.h"
#include <algorithm>

namespace velora::render {

TweenManager::TweenManager() = default;
TweenManager::~TweenManager() = default;

void TweenManager::update(double dt) {
    if (!m_enabled)
        return;

    for (auto& tw : m_tweens) {
        if (tw.finished)
            continue;

        tw.elapsed += static_cast<float>(dt);
        float t = std::clamp(tw.elapsed / tw.duration, 0.0f, 1.0f);
        float easedT = tw.easingFn(t);
        float value = tw.fromValue + (tw.toValue - tw.fromValue) * easedT;

        if (tw.onUpdate)
            tw.onUpdate(value);

        if (t >= 1.0f) {
            tw.finished = true;
            if (tw.onComplete)
                tw.onComplete();
        }
    }

    std::erase_if(m_tweens, [](const Tween& tw) { return tw.finished && tw.autoRemove; });
}

std::string TweenManager::createTween(const std::string& target, const std::string& property,
                                      float from, float to, float duration,
                                      const std::string& easingName) {
    Tween tw;
    tw.id = "tween_" + std::to_string(m_nextId++);
    tw.target = target;
    tw.property = property;
    tw.fromValue = from;
    tw.toValue = to;
    tw.duration = duration;
    tw.easingFn = easing::fromName(easingName);
    m_tweens.push_back(std::move(tw));
    return m_tweens.back().id;
}

void TweenManager::setOnUpdate(const std::string& tweenId, std::function<void(float)> fn) {
    for (auto& tw : m_tweens) {
        if (tw.id == tweenId) {
            tw.onUpdate = std::move(fn);
            return;
        }
    }
}

void TweenManager::setOnComplete(const std::string& tweenId, std::function<void()> fn) {
    for (auto& tw : m_tweens) {
        if (tw.id == tweenId) {
            tw.onComplete = std::move(fn);
            return;
        }
    }
}

void TweenManager::cancelTween(const std::string& tweenId) {
    std::erase_if(m_tweens, [&](const Tween& tw) { return tw.id == tweenId; });
}

void TweenManager::cancelAllFor(const std::string& target) {
    std::erase_if(m_tweens, [&](const Tween& tw) { return tw.target == target; });
}

void TweenManager::cancelAll() {
    m_tweens.clear();
}

} // namespace velora::render
