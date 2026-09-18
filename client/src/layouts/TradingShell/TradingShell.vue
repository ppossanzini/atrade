<script lang="ts" src="./TradingShell.ts"></script>

<template>
  <div class="app-shell">
    <el-aside class="app-shell__sidebar" width="232px">
      <div class="app-shell__brand">
        <span class="app-shell__brand-name">{{ t('app.name') }}</span>
        <span class="app-shell__brand-environment">{{ t('app.environment') }}</span>
      </div>

      <el-menu :default-active="activeRouteName" router>
        <el-menu-item
          v-for="item in navigationItems"
          :key="item.routeName"
          :index="item.routeName"
          :route="{ name: item.routeName }"
        >
          {{ t(item.labelKey) }}
        </el-menu-item>
      </el-menu>
    </el-aside>

    <div class="app-shell__body">
      <header class="app-shell__topbar">
        <span class="app-shell__operator">
          {{ t('auth.operator') }}: {{ sessionStore.operatorName }}
        </span>

        <el-button :loading="sessionStore.isBusy" @click="signOut">
          {{ t('auth.signOut') }}
        </el-button>
      </header>

      <main class="app-shell__content">
        <router-view />
      </main>
    </div>
  </div>
</template>

<style scoped lang="less" src="./TradingShell.less"></style>
