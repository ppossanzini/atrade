<script lang="ts" src="./StrategyView.ts"></script>

<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-header__title">{{ t('strategy.title') }}</h1>
        <p class="page-header__subtitle">{{ t('strategy.subtitle') }}</p>
      </div>

      <div class="inline-actions">
        <el-tag v-if="strategyStore.activeVersionNumber > 0" effect="plain">
          {{ t('strategy.versionInForce') }} v{{ strategyStore.activeVersionNumber }}
        </el-tag>
        <el-button :loading="strategyStore.isLoading" @click="refresh">
          {{ t('strategy.refresh') }}
        </el-button>
      </div>
    </div>

    <el-alert
      v-if="strategyStore.hasLoadFailure"
      class="strategy-alert"
      type="error"
      :closable="false"
      show-icon
      :title="t('common.sessionExpired')"
    />

    <el-alert
      v-if="!strategyStore.rulesInPreparation"
      class="strategy-alert"
      type="warning"
      :closable="false"
      show-icon
      :title="t('strategy.noActiveBasket')"
    />

    <template v-else>
      <el-row :gutter="16" class="strategy-grid">
        <el-col :xs="24" :lg="14">
          <div class="panel strategy-panel">
            <div class="panel-header">
              <span class="panel-title">{{ t('strategy.rules') }}</span>
              <el-tag
                v-if="strategyStore.hasPendingChanges"
                type="warning"
                effect="plain"
                size="small"
              >
                {{ t('strategy.pendingPublication') }}
              </el-tag>
            </div>

            <p class="strategy-hint">{{ t('strategy.rulesHint') }}</p>

            <el-form label-position="top">
              <el-form-item :label="t('strategy.mode')">
                <el-select v-model="entryMode" :disabled="strategyStore.isSaving">
                  <el-option
                    v-for="option in entryModeOptions"
                    :key="option.value"
                    :label="option.label"
                    :value="option.value"
                  />
                </el-select>
              </el-form-item>

              <el-row :gutter="16">
                <el-col :xs="24" :sm="12">
                  <el-form-item :label="t('strategy.riskBasket')">
                    <el-input-number
                      v-model="riskPerBasket"
                      :disabled="strategyStore.isSaving"
                      :min="0.1"
                      :max="10"
                      :step="0.1"
                      :precision="2"
                      controls-position="right"
                    />
                  </el-form-item>
                </el-col>
                <el-col :xs="24" :sm="12">
                  <el-form-item :label="t('strategy.dailyLoss')">
                    <el-input-number
                      v-model="dailyLossLimit"
                      :disabled="strategyStore.isSaving"
                      :min="0.1"
                      :max="20"
                      :step="0.5"
                      :precision="2"
                      controls-position="right"
                    />
                  </el-form-item>
                </el-col>
              </el-row>

              <el-row :gutter="16">
                <el-col :xs="24" :sm="12">
                  <el-form-item :label="t('strategy.minimumCoverage')">
                    <el-input-number
                      v-model="minimumCoverage"
                      :disabled="strategyStore.isSaving"
                      :min="50"
                      :max="100"
                      :step="5"
                      controls-position="right"
                    />
                  </el-form-item>
                </el-col>
                <el-col :xs="24" :sm="12">
                  <el-form-item :label="t('strategy.failurePolicy')">
                    <el-select v-model="failurePolicy" :disabled="strategyStore.isSaving">
                      <el-option
                        v-for="option in failurePolicyOptions"
                        :key="option.value"
                        :label="option.label"
                        :value="option.value"
                      />
                    </el-select>
                  </el-form-item>
                </el-col>
              </el-row>
            </el-form>

            <el-alert
              class="strategy-alert"
              type="warning"
              :closable="false"
              show-icon
              :title="t('strategy.guardrail')"
            />

            <el-alert
              class="strategy-alert"
              type="info"
              :closable="false"
              show-icon
              :title="t('strategy.modeScope')"
            />

            <div class="inline-actions strategy-actions">
              <el-button type="primary" :loading="strategyStore.isSaving" @click="save">
                {{ t('strategy.save') }}
              </el-button>
            </div>
          </div>

          <div class="panel strategy-panel">
            <div class="panel-header">
              <span class="panel-title">{{ t('strategy.inForce') }}</span>
              <span class="strategy-hint">{{ t('strategy.inForceHint') }}</span>
            </div>

            <template v-if="strategyStore.rulesInForce">
              <div class="metric-row">
                <span class="metric-row__label">{{ t('strategy.mode') }}</span>
                <span class="metric-row__value">
                  {{ t(`entryMode.${strategyStore.rulesInForce.entryMode}`) }}
                </span>
              </div>
              <div class="metric-row">
                <span class="metric-row__label">{{ t('strategy.riskBasket') }}</span>
                <span class="metric-row__value">
                  {{ formatNumber(strategyStore.rulesInForce.riskPerBasket, 2) }} %
                </span>
              </div>
              <div class="metric-row">
                <span class="metric-row__label">{{ t('strategy.dailyLoss') }}</span>
                <span class="metric-row__value">
                  {{ formatNumber(strategyStore.rulesInForce.dailyLossLimit, 2) }} %
                </span>
              </div>
              <div class="metric-row">
                <span class="metric-row__label">{{ t('strategy.minimumCoverage') }}</span>
                <span class="metric-row__value">
                  {{ strategyStore.rulesInForce.minimumCoverage }} %
                </span>
              </div>
              <div class="metric-row">
                <span class="metric-row__label">{{ t('strategy.failurePolicy') }}</span>
                <span class="metric-row__value">
                  {{ t(`failurePolicy.${strategyStore.rulesInForce.failurePolicy}`) }}
                </span>
              </div>
            </template>

            <p v-else class="panel-empty">{{ t('strategy.noVersionInForce') }}</p>
          </div>
        </el-col>

        <el-col :xs="24" :lg="10">
          <div class="panel strategy-panel">
            <div class="panel-header">
              <span class="panel-title">{{ t('strategy.promotion') }}</span>
              <el-tag
                :type="strategyStore.promotion?.isLiveEligible ? 'success' : 'danger'"
                size="small"
                effect="plain"
              >
                {{
                  strategyStore.promotion?.isLiveEligible
                    ? t('strategy.promotionOpen')
                    : t('strategy.promotionClosed')
                }}
              </el-tag>
            </div>

            <p class="strategy-hint">{{ t('strategy.promotionHint') }}</p>

            <div
              v-for="requirement in strategyStore.promotion?.requirements ?? []"
              :key="requirement.key"
              class="strategy-requirement"
            >
              <div class="strategy-requirement__head">
                <span class="strategy-requirement__name">
                  {{ t(`promotionRequirement.${requirement.key}`) }}
                </span>
                <el-tag :type="requirementTag(requirement.state)" size="small" effect="plain">
                  {{ t(`promotionState.${requirement.state}`) }}
                </el-tag>
              </div>
              <small class="strategy-requirement__note">
                {{ t(`promotionRequirementNote.${requirement.key}`) }}
              </small>
              <small v-if="requirement.evidence" class="strategy-requirement__evidence">
                {{ t('strategy.observed') }}: {{ requirement.evidence }}
              </small>
            </div>

            <p v-if="!strategyStore.promotion" class="panel-empty">
              {{ t('strategy.promotionUnavailable') }}
            </p>
          </div>
        </el-col>
      </el-row>
    </template>
  </div>
</template>

<style scoped lang="less" src="./StrategyView.less"></style>
