
#pragma once

#include <chrono>

namespace velora::core {

class TimeManager {
  public:
    explicit TimeManager(int targetFPS = 60);

    void reset();
    void beginFrame();
    void endFrame();

    [[nodiscard]] double getFixedDeltaTime() const { return m_fixedDt; }
    [[nodiscard]] double getDeltaTime() const { return m_deltaTime; }
    [[nodiscard]] double getTotalTime() const { return m_totalTime; }
    [[nodiscard]] double getFPS() const { return m_fps; }
    [[nodiscard]] double getTimeScale() const { return m_timeScale; }

    void setTimeScale(double scale) { m_timeScale = scale; }

  private:
    using Clock = std::chrono::steady_clock;
    using TimePoint = Clock::time_point;

    double m_fixedDt;
    double m_deltaTime = 0.0;
    double m_totalTime = 0.0;
    double m_fps = 0.0;
    double m_timeScale = 1.0;
    TimePoint m_lastFrame;
    TimePoint m_frameStart;

    double m_fpsAccumulator = 0.0;
    int m_fpsFrameCount = 0;
};

} // namespace velora::core
