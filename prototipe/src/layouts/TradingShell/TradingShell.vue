<script src="./TradingShell.js"></script>

<template>
  <div class="trading-shell">
    <aside class="trading-shell__sidebar">
      <div class="trading-shell__brand">
        <span class="trading-shell__mark">BC</span>
        <div>
          <strong>{{ t('app.name') }}</strong>
          <small>{{ t('app.environment') }}</small>
        </div>
      </div>

      <el-menu :default-active="store.activeSection" @select="store.selectSection">
        <el-menu-item v-for="item in navigation" :key="item.id" :index="item.id">
          <el-icon><component :is="item.icon" /></el-icon>
          <span>{{ t(item.label) }}</span>
        </el-menu-item>
      </el-menu>

      <div class="trading-shell__system">
        <span class="section-label">{{ t('shell.system') }}</span>
        <div class="system-row"><i class="status-dot status-dot--off"></i>{{ t('app.connection') }}</div>
        <div class="system-row"><i class="status-dot status-dot--on"></i>{{ t('shell.ragReady') }}</div>
        <div class="system-row"><i class="status-dot status-dot--on"></i>{{ t('shell.localModel') }}</div>
      </div>
    </aside>

    <section class="trading-shell__workspace">
      <header class="trading-shell__header">
        <div>
          <span class="section-label">{{ t(`navigation.${store.activeSection}`) }}</span>
          <strong>{{ t('shell.activeBasket') }}: {{ store.activeBasket.name }} <span>v{{ store.activeBasket.activeVersion }}</span></strong>
        </div>
        <div class="trading-shell__header-actions">
          <el-tag effect="plain" type="warning">{{ t('shell.paperMode') }}</el-tag>
          <el-button :icon="VideoPause" plain>{{ t('shell.closeOnly') }}</el-button>
          <el-button :icon="SwitchButton" type="danger" plain>{{ t('shell.killSwitch') }}</el-button>
        </div>
      </header>
      <main class="trading-shell__content">
        <slot />
      </main>
    </section>
  </div>
</template>

<style scoped lang="less" src="./TradingShell.less"></style>