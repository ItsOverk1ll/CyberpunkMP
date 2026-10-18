#include "ThreadService.h"

void ThreadService::RunInMainThread(std::function<void()> aFunc)
{
    auto pService = Core::Container::Get<ThreadService>();
    pService->m_queue.Add(aFunc);
}

void ThreadService::OnGameUpdate(RED4ext::CGameApplication* apApp)
{
    // ChoomCoop: run each task outside the queue lock (Drain() holds it), so a task may
    // queue another without deadlocking. See docs/DECISIONS.md.
    std::function<void()> func;
    while (m_queue.Pop(func))
        func();
}
