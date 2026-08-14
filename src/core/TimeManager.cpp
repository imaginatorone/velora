
#include "TimeManager.h"

namespace velora::core {

TimeManager::TimeManager(int targetFPS)
    : m_fixedDt(1.0 / static_cast<double>(targetFPS)), m_lastFrame(Clock::now()),
      m_frameStart(Clock::now()) {}

void TimeManager::reset() {
    m_lastFrame = Clock::now();
    m_deltaTime = 0.0;
    m_totalTime = 0.0;
    m_fps = 0.0;
    m_fpsAccumulator = 0.0;
    m_fpsFrameCount = 0;
}

void TimeManager::beginFrame() {
    m_frameStart = Clock::now();
    auto elapsed = std::chrono::duration<double>(m_frameStart - m_lastFrame);
    m_deltaTime = elapsed.count() * m_timeScale;
    m_totalTime += m_deltaTime;
    m_lastFrame = m_frameStart;
}

void TimeManager::endFrame() {
    m_fpsFrameCount++;
    auto now = Clock::now();
    m_fpsAccumulator += std::chrono::duration<double>(now - m_frameStart).count();
    if (m_fpsAccumulator >= 1.0) {
        m_fps = static_cast<double>(m_fpsFrameCount) / m_fpsAccumulator;
        m_fpsAccumulator = 0.0;
        m_fpsFrameCount = 0;
    }
}

} // namespace velora::core
